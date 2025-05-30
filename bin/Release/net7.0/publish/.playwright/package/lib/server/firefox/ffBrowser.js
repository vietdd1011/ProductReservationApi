"use strict";

Object.defineProperty(exports, "__esModule", {
  value: true
});
exports.FFBrowserContext = exports.FFBrowser = void 0;
var _utils = require("../../utils");
var _browser = require("../browser");
var _browserContext = require("../browserContext");
var _errors = require("../errors");
var network = _interopRequireWildcard(require("../network"));
var _page = require("../page");
var _ffConnection = require("./ffConnection");
var _ffPage = require("./ffPage");
function _getRequireWildcardCache(e) { if ("function" != typeof WeakMap) return null; var r = new WeakMap(), t = new WeakMap(); return (_getRequireWildcardCache = function (e) { return e ? t : r; })(e); }
function _interopRequireWildcard(e, r) { if (!r && e && e.__esModule) return e; if (null === e || "object" != typeof e && "function" != typeof e) return { default: e }; var t = _getRequireWildcardCache(r); if (t && t.has(e)) return t.get(e); var n = { __proto__: null }, a = Object.defineProperty && Object.getOwnPropertyDescriptor; for (var u in e) if ("default" !== u && {}.hasOwnProperty.call(e, u)) { var i = a ? Object.getOwnPropertyDescriptor(e, u) : null; i && (i.get || i.set) ? Object.defineProperty(n, u, i) : n[u] = e[u]; } return n.default = e, t && t.set(e, n), n; }
/**
 * Copyright 2018 Google Inc. All rights reserved.
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the 'License');
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an 'AS IS' BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

class FFBrowser extends _browser.Browser {
  static async connect(parent, transport, options) {
    var _options$originalLaun;
    const connection = new _ffConnection.FFConnection(transport, options.protocolLogger, options.browserLogsCollector);
    const browser = new FFBrowser(parent, connection, options);
    if (options.__testHookOnConnectToBrowser) await options.__testHookOnConnectToBrowser();
    let firefoxUserPrefs = (_options$originalLaun = options.originalLaunchOptions.firefoxUserPrefs) !== null && _options$originalLaun !== void 0 ? _options$originalLaun : {};
    if (Object.keys(kBandaidFirefoxUserPrefs).length) firefoxUserPrefs = {
      ...kBandaidFirefoxUserPrefs,
      ...firefoxUserPrefs
    };
    const promises = [browser.session.send('Browser.enable', {
      attachToDefaultContext: !!options.persistent,
      userPrefs: Object.entries(firefoxUserPrefs).map(([name, value]) => ({
        name,
        value
      }))
    }), browser._initVersion()];
    if (options.persistent) {
      browser._defaultContext = new FFBrowserContext(browser, undefined, options.persistent);
      promises.push(browser._defaultContext._initialize());
    }
    const proxy = options.originalLaunchOptions.proxyOverride || options.proxy;
    if (proxy) promises.push(browser.session.send('Browser.setBrowserProxy', toJugglerProxyOptions(proxy)));
    await Promise.all(promises);
    return browser;
  }
  constructor(parent, connection, options) {
    super(parent, options);
    this._connection = void 0;
    this.session = void 0;
    this._ffPages = void 0;
    this._contexts = void 0;
    this._version = '';
    this._userAgent = '';
    this._connection = connection;
    this.session = connection.rootSession;
    this._ffPages = new Map();
    this._contexts = new Map();
    this._connection.on(_ffConnection.ConnectionEvents.Disconnected, () => this._onDisconnect());
    this.session.on('Browser.attachedToTarget', this._onAttachedToTarget.bind(this));
    this.session.on('Browser.detachedFromTarget', this._onDetachedFromTarget.bind(this));
    this.session.on('Browser.downloadCreated', this._onDownloadCreated.bind(this));
    this.session.on('Browser.downloadFinished', this._onDownloadFinished.bind(this));
    this.session.on('Browser.videoRecordingFinished', this._onVideoRecordingFinished.bind(this));
  }
  async _initVersion() {
    const result = await this.session.send('Browser.getInfo');
    this._version = result.version.substring(result.version.indexOf('/') + 1);
    this._userAgent = result.userAgent;
  }
  isConnected() {
    return !this._connection._closed;
  }
  async doCreateNewContext(options) {
    if (options.isMobile) throw new Error('options.isMobile is not supported in Firefox');
    const {
      browserContextId
    } = await this.session.send('Browser.createBrowserContext', {
      removeOnDetach: true
    });
    const context = new FFBrowserContext(this, browserContextId, options);
    await context._initialize();
    this._contexts.set(browserContextId, context);
    return context;
  }
  contexts() {
    return Array.from(this._contexts.values());
  }
  version() {
    return this._version;
  }
  userAgent() {
    return this._userAgent;
  }
  _onDetachedFromTarget(payload) {
    const ffPage = this._ffPages.get(payload.targetId);
    this._ffPages.delete(payload.targetId);
    ffPage.didClose();
  }
  _onAttachedToTarget(payload) {
    const {
      targetId,
      browserContextId,
      openerId,
      type
    } = payload.targetInfo;
    (0, _utils.assert)(type === 'page');
    const context = browserContextId ? this._contexts.get(browserContextId) : this._defaultContext;
    (0, _utils.assert)(context, `Unknown context id:${browserContextId}, _defaultContext: ${this._defaultContext}`);
    const session = this._connection.createSession(payload.sessionId);
    const opener = openerId ? this._ffPages.get(openerId) : null;
    const ffPage = new _ffPage.FFPage(session, context, opener);
    this._ffPages.set(targetId, ffPage);
  }
  _onDownloadCreated(payload) {
    const ffPage = this._ffPages.get(payload.pageTargetId);
    if (!ffPage) return;

    // Abort the navigation that turned into download.
    ffPage._page._frameManager.frameAbortedNavigation(payload.frameId, 'Download is starting');
    let originPage = ffPage._page.initializedOrUndefined();
    // If it's a new window download, report it on the opener page.
    if (!originPage) {
      // Resume the page creation with an error. The page will automatically close right
      // after the download begins.
      ffPage._markAsError(new Error('Starting new page download'));
      if (ffPage._opener) originPage = ffPage._opener._page.initializedOrUndefined();
    }
    if (!originPage) return;
    this._downloadCreated(originPage, payload.uuid, payload.url, payload.suggestedFileName);
  }
  _onDownloadFinished(payload) {
    const error = payload.canceled ? 'canceled' : payload.error;
    this._downloadFinished(payload.uuid, error);
  }
  _onVideoRecordingFinished(payload) {
    var _this$_takeVideo;
    (_this$_takeVideo = this._takeVideo(payload.screencastId)) === null || _this$_takeVideo === void 0 || _this$_takeVideo.reportFinished();
  }
  _onDisconnect() {
    for (const video of this._idToVideo.values()) video.artifact.reportFinished(new _errors.TargetClosedError());
    this._idToVideo.clear();
    for (const ffPage of this._ffPages.values()) ffPage.didClose();
    this._ffPages.clear();
    this._didClose();
  }
}
exports.FFBrowser = FFBrowser;
class FFBrowserContext extends _browserContext.BrowserContext {
  constructor(browser, browserContextId, options) {
    super(browser, options, browserContextId);
  }
  async _initialize() {
    (0, _utils.assert)(!this._ffPages().length);
    const browserContextId = this._browserContextId;
    const promises = [super._initialize(), this._browser.session.send('Browser.addBinding', {
      browserContextId: this._browserContextId,
      name: _page.PageBinding.kPlaywrightBinding,
      script: ''
    })];
    if (this._options.acceptDownloads !== 'internal-browser-default') {
      promises.push(this._browser.session.send('Browser.setDownloadOptions', {
        browserContextId,
        downloadOptions: {
          behavior: this._options.acceptDownloads === 'accept' ? 'saveToDisk' : 'cancel',
          downloadsDir: this._browser.options.downloadsPath
        }
      }));
    }
    if (this._options.viewport) {
      const viewport = {
        viewportSize: {
          width: this._options.viewport.width,
          height: this._options.viewport.height
        },
        deviceScaleFactor: this._options.deviceScaleFactor || 1
      };
      promises.push(this._browser.session.send('Browser.setDefaultViewport', {
        browserContextId,
        viewport
      }));
    }
    if (this._options.hasTouch) promises.push(this._browser.session.send('Browser.setTouchOverride', {
      browserContextId,
      hasTouch: true
    }));
    if (this._options.userAgent) promises.push(this._browser.session.send('Browser.setUserAgentOverride', {
      browserContextId,
      userAgent: this._options.userAgent
    }));
    if (this._options.bypassCSP) promises.push(this._browser.session.send('Browser.setBypassCSP', {
      browserContextId,
      bypassCSP: true
    }));
    if (this._options.ignoreHTTPSErrors || this._options.internalIgnoreHTTPSErrors) promises.push(this._browser.session.send('Browser.setIgnoreHTTPSErrors', {
      browserContextId,
      ignoreHTTPSErrors: true
    }));
    if (this._options.javaScriptEnabled === false) promises.push(this._browser.session.send('Browser.setJavaScriptDisabled', {
      browserContextId,
      javaScriptDisabled: true
    }));
    if (this._options.locale) promises.push(this._browser.session.send('Browser.setLocaleOverride', {
      browserContextId,
      locale: this._options.locale
    }));
    if (this._options.timezoneId) promises.push(this._browser.session.send('Browser.setTimezoneOverride', {
      browserContextId,
      timezoneId: this._options.timezoneId
    }));
    if (this._options.extraHTTPHeaders || this._options.locale) promises.push(this.setExtraHTTPHeaders(this._options.extraHTTPHeaders || []));
    if (this._options.httpCredentials) promises.push(this.setHTTPCredentials(this._options.httpCredentials));
    if (this._options.geolocation) promises.push(this.setGeolocation(this._options.geolocation));
    if (this._options.offline) promises.push(this.setOffline(this._options.offline));
    if (this._options.colorScheme !== 'no-override') {
      promises.push(this._browser.session.send('Browser.setColorScheme', {
        browserContextId,
        colorScheme: this._options.colorScheme !== undefined ? this._options.colorScheme : 'light'
      }));
    }
    if (this._options.reducedMotion !== 'no-override') {
      promises.push(this._browser.session.send('Browser.setReducedMotion', {
        browserContextId,
        reducedMotion: this._options.reducedMotion !== undefined ? this._options.reducedMotion : 'no-preference'
      }));
    }
    if (this._options.forcedColors !== 'no-override') {
      promises.push(this._browser.session.send('Browser.setForcedColors', {
        browserContextId,
        forcedColors: this._options.forcedColors !== undefined ? this._options.forcedColors : 'none'
      }));
    }
    if (this._options.contrast !== 'no-override') {
      promises.push(this._browser.session.send('Browser.setContrast', {
        browserContextId,
        contrast: this._options.contrast !== undefined ? this._options.contrast : 'no-preference'
      }));
    }
    if (this._options.recordVideo) {
      promises.push(this._ensureVideosPath().then(() => {
        return this._browser.session.send('Browser.setVideoRecordingOptions', {
          // validateBrowserContextOptions ensures correct video size.
          options: {
            ...this._options.recordVideo.size,
            dir: this._options.recordVideo.dir
          },
          browserContextId: this._browserContextId
        });
      }));
    }
    const proxy = this._options.proxyOverride || this._options.proxy;
    if (proxy) {
      promises.push(this._browser.session.send('Browser.setContextProxy', {
        browserContextId: this._browserContextId,
        ...toJugglerProxyOptions(proxy)
      }));
    }
    await Promise.all(promises);
  }
  _ffPages() {
    return Array.from(this._browser._ffPages.values()).filter(ffPage => ffPage._browserContext === this);
  }
  possiblyUninitializedPages() {
    return this._ffPages().map(ffPage => ffPage._page);
  }
  async doCreateNewPage() {
    (0, _browserContext.assertBrowserContextIsNotOwned)(this);
    const {
      targetId
    } = await this._browser.session.send('Browser.newPage', {
      browserContextId: this._browserContextId
    }).catch(e => {
      if (e.message.includes('Failed to override timezone')) throw new Error(`Invalid timezone ID: ${this._options.timezoneId}`);
      throw e;
    });
    return this._browser._ffPages.get(targetId)._page;
  }
  async doGetCookies(urls) {
    const {
      cookies
    } = await this._browser.session.send('Browser.getCookies', {
      browserContextId: this._browserContextId
    });
    return network.filterCookies(cookies.map(c => {
      const copy = {
        ...c
      };
      delete copy.size;
      delete copy.session;
      return copy;
    }), urls);
  }
  async addCookies(cookies) {
    const cc = network.rewriteCookies(cookies).map(c => ({
      ...c,
      expires: c.expires === -1 ? undefined : c.expires
    }));
    await this._browser.session.send('Browser.setCookies', {
      browserContextId: this._browserContextId,
      cookies: cc
    });
  }
  async doClearCookies() {
    await this._browser.session.send('Browser.clearCookies', {
      browserContextId: this._browserContextId
    });
  }
  async doGrantPermissions(origin, permissions) {
    const webPermissionToProtocol = new Map([['geolocation', 'geo'], ['persistent-storage', 'persistent-storage'], ['push', 'push'], ['notifications', 'desktop-notification']]);
    const filtered = permissions.map(permission => {
      const protocolPermission = webPermissionToProtocol.get(permission);
      if (!protocolPermission) throw new Error('Unknown permission: ' + permission);
      return protocolPermission;
    });
    await this._browser.session.send('Browser.grantPermissions', {
      origin: origin,
      browserContextId: this._browserContextId,
      permissions: filtered
    });
  }
  async doClearPermissions() {
    await this._browser.session.send('Browser.resetPermissions', {
      browserContextId: this._browserContextId
    });
  }
  async setGeolocation(geolocation) {
    (0, _browserContext.verifyGeolocation)(geolocation);
    this._options.geolocation = geolocation;
    await this._browser.session.send('Browser.setGeolocationOverride', {
      browserContextId: this._browserContextId,
      geolocation: geolocation || null
    });
  }
  async setExtraHTTPHeaders(headers) {
    this._options.extraHTTPHeaders = headers;
    let allHeaders = this._options.extraHTTPHeaders;
    if (this._options.locale) allHeaders = network.mergeHeaders([allHeaders, network.singleHeader('Accept-Language', this._options.locale)]);
    await this._browser.session.send('Browser.setExtraHTTPHeaders', {
      browserContextId: this._browserContextId,
      headers: allHeaders
    });
  }
  async setUserAgent(userAgent) {
    await this._browser.session.send('Browser.setUserAgentOverride', {
      browserContextId: this._browserContextId,
      userAgent: userAgent || null
    });
  }
  async setOffline(offline) {
    this._options.offline = offline;
    await this._browser.session.send('Browser.setOnlineOverride', {
      browserContextId: this._browserContextId,
      override: offline ? 'offline' : 'online'
    });
  }
  async doSetHTTPCredentials(httpCredentials) {
    this._options.httpCredentials = httpCredentials;
    let credentials = null;
    if (httpCredentials) {
      const {
        username,
        password,
        origin
      } = httpCredentials;
      credentials = {
        username,
        password,
        origin
      };
    }
    await this._browser.session.send('Browser.setHTTPCredentials', {
      browserContextId: this._browserContextId,
      credentials
    });
  }
  async doAddInitScript(initScript) {
    await this._updateInitScripts();
  }
  async doRemoveNonInternalInitScripts() {
    await this._updateInitScripts();
  }
  async _updateInitScripts() {
    const bindingScripts = [...this._pageBindings.values()].map(binding => binding.initScript.source);
    const initScripts = this.initScripts.map(script => script.source);
    await this._browser.session.send('Browser.setInitScripts', {
      browserContextId: this._browserContextId,
      scripts: [...bindingScripts, ...initScripts].map(script => ({
        script
      }))
    });
  }
  async doUpdateRequestInterception() {
    await Promise.all([this._browser.session.send('Browser.setRequestInterception', {
      browserContextId: this._browserContextId,
      enabled: !!this._requestInterceptor
    }), this._browser.session.send('Browser.setCacheDisabled', {
      browserContextId: this._browserContextId,
      cacheDisabled: !!this._requestInterceptor
    })]);
  }
  onClosePersistent() {}
  async clearCache() {
    // Clearing only the context cache does not work: https://bugzilla.mozilla.org/show_bug.cgi?id=1819147
    await this._browser.session.send('Browser.clearCache');
  }
  async doClose(reason) {
    if (!this._browserContextId) {
      if (this._options.recordVideo) {
        await this._browser.session.send('Browser.setVideoRecordingOptions', {
          options: undefined,
          browserContextId: this._browserContextId
        });
      }
      // Closing persistent context should close the browser.
      await this._browser.close({
        reason
      });
    } else {
      await this._browser.session.send('Browser.removeBrowserContext', {
        browserContextId: this._browserContextId
      });
      this._browser._contexts.delete(this._browserContextId);
    }
  }
  async cancelDownload(uuid) {
    await this._browser.session.send('Browser.cancelDownload', {
      uuid
    });
  }
}
exports.FFBrowserContext = FFBrowserContext;
function toJugglerProxyOptions(proxy) {
  const proxyServer = new URL(proxy.server);
  let port = parseInt(proxyServer.port, 10);
  let type = 'http';
  if (proxyServer.protocol === 'socks5:') type = 'socks';else if (proxyServer.protocol === 'socks4:') type = 'socks4';else if (proxyServer.protocol === 'https:') type = 'https';
  if (proxyServer.port === '') {
    if (proxyServer.protocol === 'http:') port = 80;else if (proxyServer.protocol === 'https:') port = 443;
  }
  return {
    type,
    bypass: proxy.bypass ? proxy.bypass.split(',').map(domain => domain.trim()) : [],
    host: proxyServer.hostname,
    port,
    username: proxy.username,
    password: proxy.password
  };
}

// Prefs for quick fixes that didn't make it to the build.
// Should all be moved to `playwright.cfg`.
const kBandaidFirefoxUserPrefs = {
  'dom.fetchKeepalive.enabled': false
};