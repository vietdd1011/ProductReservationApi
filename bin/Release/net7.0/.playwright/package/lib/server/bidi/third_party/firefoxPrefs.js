"use strict";

Object.defineProperty(exports, "__esModule", {
  value: true
});
exports.createProfile = createProfile;
var _fs = _interopRequireDefault(require("fs"));
var _path = _interopRequireDefault(require("path"));
function _interopRequireDefault(e) { return e && e.__esModule ? e : { default: e }; }
/**
 * @license
 * Copyright 2023 Google Inc.
 * SPDX-License-Identifier: Apache-2.0
 */

/* eslint-disable curly, indent */

async function createProfile(options) {
  if (!_fs.default.existsSync(options.path)) {
    await _fs.default.promises.mkdir(options.path, {
      recursive: true
    });
  }
  await writePreferences({
    preferences: {
      ...defaultProfilePreferences(options.preferences),
      ...options.preferences
    },
    path: options.path
  });
}
function defaultProfilePreferences(extraPrefs) {
  const server = 'dummy.test';
  const defaultPrefs = {
    // Make sure Shield doesn't hit the network.
    'app.normandy.api_url': '',
    // Disable Firefox old build background check
    'app.update.checkInstallTime': false,
    // Disable automatically upgrading Firefox
    'app.update.disabledForTesting': true,
    // Increase the APZ content response timeout to 1 minute
    'apz.content_response_timeout': 60000,
    // Prevent various error message on the console
    // jest-puppeteer asserts that no error message is emitted by the console
    'browser.contentblocking.features.standard': '-tp,tpPrivate,cookieBehavior0,-cm,-fp',
    // Enable the dump function: which sends messages to the system
    // console
    // https://bugzilla.mozilla.org/show_bug.cgi?id=1543115
    'browser.dom.window.dump.enabled': true,
    // Make sure newtab weather doesn't hit the network to retrieve weather data.
    'browser.newtabpage.activity-stream.discoverystream.region-weather-config': '',
    // Make sure newtab wallpapers don't hit the network to retrieve wallpaper data.
    'browser.newtabpage.activity-stream.newtabWallpapers.enabled': false,
    'browser.newtabpage.activity-stream.newtabWallpapers.v2.enabled': false,
    // Make sure Topsites doesn't hit the network to retrieve sponsored tiles.
    'browser.newtabpage.activity-stream.showSponsoredTopSites': false,
    // Disable topstories
    'browser.newtabpage.activity-stream.feeds.system.topstories': false,
    // Always display a blank page
    'browser.newtabpage.enabled': false,
    // Background thumbnails in particular cause grief: and disabling
    // thumbnails in general cannot hurt
    'browser.pagethumbnails.capturing_disabled': true,
    // Disable safebrowsing components.
    'browser.safebrowsing.blockedURIs.enabled': false,
    'browser.safebrowsing.downloads.enabled': false,
    'browser.safebrowsing.malware.enabled': false,
    'browser.safebrowsing.phishing.enabled': false,
    // Disable updates to search engines.
    'browser.search.update': false,
    // Do not restore the last open set of tabs if the browser has crashed
    'browser.sessionstore.resume_from_crash': false,
    // Skip check for default browser on startup
    'browser.shell.checkDefaultBrowser': false,
    // Disable newtabpage
    'browser.startup.homepage': 'about:blank',
    // Do not redirect user when a milstone upgrade of Firefox is detected
    'browser.startup.homepage_override.mstone': 'ignore',
    // Start with a blank page about:blank
    'browser.startup.page': 0,
    // Do not allow background tabs to be zombified on Android: otherwise for
    // tests that open additional tabs: the test harness tab itself might get
    // unloaded
    'browser.tabs.disableBackgroundZombification': false,
    // Do not warn when closing all other open tabs
    'browser.tabs.warnOnCloseOtherTabs': false,
    // Do not warn when multiple tabs will be opened
    'browser.tabs.warnOnOpen': false,
    // Do not automatically offer translations, as tests do not expect this.
    'browser.translations.automaticallyPopup': false,
    // Disable the UI tour.
    'browser.uitour.enabled': false,
    // Turn off search suggestions in the location bar so as not to trigger
    // network connections.
    'browser.urlbar.suggest.searches': false,
    // Disable first run splash page on Windows 10
    'browser.usedOnWindows10.introURL': '',
    // Do not warn on quitting Firefox
    'browser.warnOnQuit': false,
    // Defensively disable data reporting systems
    'datareporting.healthreport.documentServerURI': `http://${server}/dummy/healthreport/`,
    'datareporting.healthreport.logging.consoleEnabled': false,
    'datareporting.healthreport.service.enabled': false,
    'datareporting.healthreport.service.firstRun': false,
    'datareporting.healthreport.uploadEnabled': false,
    // Do not show datareporting policy notifications which can interfere with tests
    'datareporting.policy.dataSubmissionEnabled': false,
    'datareporting.policy.dataSubmissionPolicyBypassNotification': true,
    // DevTools JSONViewer sometimes fails to load dependencies with its require.js.
    // This doesn't affect Puppeteer but spams console (Bug 1424372)
    'devtools.jsonview.enabled': false,
    // Disable popup-blocker
    'dom.disable_open_during_load': false,
    // Enable the support for File object creation in the content process
    // Required for |Page.setFileInputFiles| protocol method.
    'dom.file.createInChild': true,
    // Disable the ProcessHangMonitor
    'dom.ipc.reportProcessHangs': false,
    // Disable slow script dialogues
    'dom.max_chrome_script_run_time': 0,
    'dom.max_script_run_time': 0,
    // Disable background timer throttling to allow tests to run in parallel
    // without a decrease in performance.
    'dom.min_background_timeout_value': 0,
    'dom.min_background_timeout_value_without_budget_throttling': 0,
    'dom.timeout.enable_budget_timer_throttling': false,
    // Disable HTTPS-First upgrades
    'dom.security.https_first': false,
    // Only load extensions from the application and user profile
    // AddonManager.SCOPE_PROFILE + AddonManager.SCOPE_APPLICATION
    'extensions.autoDisableScopes': 0,
    'extensions.enabledScopes': 5,
    // Disable metadata caching for installed add-ons by default
    'extensions.getAddons.cache.enabled': false,
    // Disable installing any distribution extensions or add-ons.
    'extensions.installDistroAddons': false,
    // Disabled screenshots extension
    'extensions.screenshots.disabled': true,
    // Turn off extension updates so they do not bother tests
    'extensions.update.enabled': false,
    // Turn off extension updates so they do not bother tests
    'extensions.update.notifyUser': false,
    // Make sure opening about:addons will not hit the network
    'extensions.webservice.discoverURL': `http://${server}/dummy/discoveryURL`,
    // Allow the application to have focus even it runs in the background
    'focusmanager.testmode': true,
    // Disable useragent updates
    'general.useragent.updates.enabled': false,
    // Always use network provider for geolocation tests so we bypass the
    // macOS dialog raised by the corelocation provider
    'geo.provider.testing': true,
    // Do not scan Wifi
    'geo.wifi.scan': false,
    // No hang monitor
    'hangmonitor.timeout': 0,
    // Show chrome errors and warnings in the error console
    'javascript.options.showInConsole': true,
    // Do not throttle rendering (requestAnimationFrame) in background tabs
    'layout.testing.top-level-always-active': true,
    // Disable download and usage of OpenH264: and Widevine plugins
    'media.gmp-manager.updateEnabled': false,
    // Disable the GFX sanity window
    'media.sanity-test.disabled': true,
    // Disable connectivity service pings
    'network.connectivity-service.enabled': false,
    // Disable experimental feature that is only available in Nightly
    'network.cookie.sameSite.laxByDefault': false,
    // Do not prompt for temporary redirects
    'network.http.prompt-temp-redirect': false,
    // Disable speculative connections so they are not reported as leaking
    // when they are hanging around
    'network.http.speculative-parallel-limit': 0,
    // Do not automatically switch between offline and online
    'network.manage-offline-status': false,
    // Make sure SNTP requests do not hit the network
    'network.sntp.pools': server,
    // Disable Flash.
    'plugin.state.flash': 0,
    'privacy.trackingprotection.enabled': false,
    // Can be removed once Firefox 89 is no longer supported
    // https://bugzilla.mozilla.org/show_bug.cgi?id=1710839
    'remote.enabled': true,
    // Don't do network connections for mitm priming
    'security.certerrors.mitm.priming.enabled': false,
    // Local documents have access to all other local documents,
    // including directory listings
    'security.fileuri.strict_origin_policy': false,
    // Do not wait for the notification button security delay
    'security.notification_enable_delay': 0,
    // Do not automatically fill sign-in forms with known usernames and
    // passwords
    'signon.autofillForms': false,
    // Disable password capture, so that tests that include forms are not
    // influenced by the presence of the persistent doorhanger notification
    'signon.rememberSignons': false,
    // Disable first-run welcome page
    'startup.homepage_welcome_url': 'about:blank',
    // Disable first-run welcome page
    'startup.homepage_welcome_url.additional': '',
    // Disable browser animations (tabs, fullscreen, sliding alerts)
    'toolkit.cosmeticAnimations.enabled': false,
    // Prevent starting into safe mode after application crashes
    'toolkit.startup.max_resumed_crashes': -1
  };
  return Object.assign(defaultPrefs, extraPrefs);
}

/**
 * Populates the user.js file with custom preferences as needed to allow
 * Firefox's CDP support to properly function. These preferences will be
 * automatically copied over to prefs.js during startup of Firefox. To be
 * able to restore the original values of preferences a backup of prefs.js
 * will be created.
 *
 * @param prefs - List of preferences to add.
 * @param profilePath - Firefox profile to write the preferences to.
 */
async function writePreferences(options) {
  const prefsPath = _path.default.join(options.path, 'prefs.js');
  const lines = Object.entries(options.preferences).map(([key, value]) => {
    return `user_pref(${JSON.stringify(key)}, ${JSON.stringify(value)});`;
  });

  // Use allSettled to prevent corruption
  const result = await Promise.allSettled([_fs.default.promises.writeFile(_path.default.join(options.path, 'user.js'), lines.join('\n')),
  // Create a backup of the preferences file if it already exitsts.
  _fs.default.promises.access(prefsPath, _fs.default.constants.F_OK).then(async () => {
    await _fs.default.promises.copyFile(prefsPath, _path.default.join(options.path, 'prefs.js.playwright'));
  },
  // Swallow only if file does not exist
  () => {})]);
  for (const command of result) {
    if (command.status === 'rejected') {
      throw command.reason;
    }
  }
}