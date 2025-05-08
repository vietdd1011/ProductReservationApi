using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using ProductReservationApi.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var storageFilePath = Path.Combine(builder.Environment.ContentRootPath, "storageState.json");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowRFTools", policy =>
    {
        policy.WithOrigins("http://rftools.local")
              .AllowAnyHeader()
              .AllowAnyMethod();
        policy.WithOrigins("https://rftools.royalfashion.pl")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
var app = builder.Build();
app.UseCors("AllowRFTools");
app.MapGet("/home", () =>
{
    return Results.Ok(new { msg = "ok" });
});

app.MapPost("/reservation", async () =>
{
    try
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage" }
        });

        IBrowserContext contextBrowser;
        if (File.Exists(storageFilePath))
        {
            contextBrowser = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storageFilePath,
                BypassCSP = true,
                RecordVideoDir = null,
                ViewportSize = null,
                BaseURL = "https://client4901.idosell.com",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            });
        }
        else
        {
            contextBrowser = await browser.NewContextAsync();
        }
        await contextBrowser.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            if (req.ResourceType == "image" || req.ResourceType == "font" || req.ResourceType == "stylesheet")
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });

        var page = await contextBrowser.NewPageAsync();
        var targetUrl = "https://client4901.idosell.com/panel/product-aside.php?action=view&mode=3&stock=2&order_stock=5";
        await page.GotoAsync(targetUrl);
        bool isLoginFormVisible = await page.Locator("#panel_login").IsVisibleAsync();
        if (isLoginFormVisible)
        {
            await page.FillAsync("#panel_login", "vietdao");
            await page.FillAsync("#panel_password", "Abc@12345");
            await page.ClickAsync("button[type=submit]");
            // Lưu lại storage state để dùng cho lần sau
            await contextBrowser.StorageStateAsync(new BrowserContextStorageStateOptions { Path = "storageState.json" });
            await page.GotoAsync(targetUrl);
        }
        else
        {
            Console.WriteLine("Session còn hiệu lực. Đã đăng nhập.");
        }

        var rows = await page.QuerySelectorAllAsync("table tbody tr");
        var data = new ConcurrentBag<object>(); // thread-safe collection

        await Task.WhenAll(rows.Select(async row =>
        {
            var cells = await row.QuerySelectorAllAsync("td");
            if (cells.Count >= 4)
            {
                var productCodeElement = await cells[1].QuerySelectorAsync("a");
                var productCode = productCodeElement != null ? await productCodeElement.InnerTextAsync() : "";

                var orderElements = await cells[3].QuerySelectorAllAsync("span");
                foreach (var o in orderElements)
                {
                    var text = await o.InnerTextAsync();

                    var quantityMatch = Regex.Match(text, @"-\s*(\d+)\s+\w+");
                    var quantity = quantityMatch.Success ? quantityMatch.Groups[1].Value : "0";
                    var orderType = text.ToLower().Contains("shein") ? "shein" : "royal";

                    var orderNumberMatch = Regex.Match(text, @"\]\s*(\d+)\s*\(");
                    var orderNumber = orderNumberMatch.Success ? orderNumberMatch.Groups[1].Value : "";

                    if (!string.IsNullOrEmpty(productCode))
                    {
                        data.Add(new { productCode, quantity, orderType, orderNumber });
                    }
                }
            }
        }));
        await browser.CloseAsync();
        return Results.Ok(new { reservation = data });
    }
    catch (Exception ex)
    {
        return Results.Problem("Đã có lỗi xảy ra: " + ex.Message);
    }
});

app.MapPost("/update-reservation", async (HttpContext context) =>
{
    try
    {
        // Đọc body từ request
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<ReservationPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var reservations = payload?.Reservations ?? new List<Reservation>();
        var docId = payload?.DocId ?? 0;

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage" }
        });

        IBrowserContext contextBrowser;
        if (File.Exists(storageFilePath))
        {
            contextBrowser = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storageFilePath,
                BypassCSP = true,
                RecordVideoDir = null,
                ViewportSize = null,
                BaseURL = "https://client4901.idosell.com",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            });
        }
        else
        {
            contextBrowser = await browser.NewContextAsync();
        }
        await contextBrowser.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            if (req.ResourceType == "image" || req.ResourceType == "font" || req.ResourceType == "stylesheet")
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        var page = await contextBrowser.NewPageAsync();
        var targetUrl = "https://client4901.idosell.com/panel/stocks-dislocate.php?action=edit&document_id=" + docId;
        await page.GotoAsync(targetUrl);
        bool isLoginFormVisible = await page.Locator("#panel_login").IsVisibleAsync();
        if (isLoginFormVisible)
        {
            await page.FillAsync("#panel_login", "vietdao");
            await page.FillAsync("#panel_password", "Abc@12345");
            await page.ClickAsync("button[type=submit]");
            // Lưu lại storage state để dùng cho lần sau
            await contextBrowser.StorageStateAsync(new BrowserContextStorageStateOptions { Path = "storageState.json" });
            await page.GotoAsync(targetUrl);
        }
        else
        {
            Console.WriteLine("Session còn hiệu lực. Đã đăng nhập.");
        }

        // Chọn table summary trong div có id stock-products-document
        var table = await page.QuerySelectorAsync("div#stock-products-document table[summary]");
        if (table == null)
        {
            throw new Exception("Không tìm thấy table summary trong div#stock-products-document");
        }

        var rows = await table.QuerySelectorAllAsync("tbody > tr");

        // Tìm theo productCode và orderNumber để click đúng số lượng
        var removedRow = new List<string>();
        foreach (var res in reservations)
        {
            foreach (var row in rows)
            {
                var codeCell = await row.QuerySelectorAsync("td:nth-child(3)");
                if (codeCell == null) continue;

                var text = await codeCell.InnerTextAsync();
                var code = text.Split('\n')[0].Trim();

                if (code == res.ProductCode)
                {
                    // Duyệt các dòng đơn hàng trong cột Quantity (cột 5)
                    var nestedRows = await row.QuerySelectorAllAsync("td:nth-child(5) table tbody tr");
                    foreach (var orderRow in nestedRows)
                    {
                        // Lấy số order
                        var anchor = await orderRow.QuerySelectorAsync("a");
                        var orderText = anchor != null ? (await anchor.InnerTextAsync()).Trim() : string.Empty;

                        if (orderText == res.OrderNumber)
                        {
                            // Lấy nút Add tương ứng trong dòng đó
                            var img = await orderRow.QuerySelectorAsync("img[alt='Add to invoice'], img[alt='Dodaj do faktury']");
                            if (img != null)
                            {
                                await img.WaitForElementStateAsync(ElementState.Visible);
                                // Click số lần bằng quantity
                                for (int i = 0; i < int.Parse(res.Quantity); i++)
                                {
                                    await img.ClickAsync();
                                }
                            }
                        }
                    }
                }
            }
        }
        try
        {
            var saveButton = await page.WaitForSelectorAsync("#fg_save_order", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });

            if (saveButton != null)
            {
                await saveButton.ClickAsync();
            }
        }
        catch (TimeoutException)
        {
            // Không tìm thấy nút trong thời gian cho phép -> bỏ qua
        }
        await Task.Delay(1000);

        await page.WaitForSelectorAsync("#msg_line_msg", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await page.WaitForSelectorAsync("div#stock-products-document table[summary]", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        table = await page.QuerySelectorAsync("div#stock-products-document table[summary]");
        if (table == null)
        {
            throw new Exception("Không tìm thấy table summary trong div#stock-products-document");
        }

        rows = await table.QuerySelectorAllAsync("tbody > tr");
        foreach (var res in reservations)
        {
            foreach (var row in rows)
            {
                var codeCell = await row.QuerySelectorAsync("td:nth-child(3)");
                if (codeCell == null) continue;

                var text = await codeCell.InnerTextAsync();
                var code = text.Split('\n')[0].Trim();

                if (code == res.ProductCode)
                {
                    // Duyệt các dòng đơn hàng trong cột Quantity (cột 5)
                    var nestedRows = await row.QuerySelectorAllAsync("td:nth-child(5) table tbody tr");
                    foreach (var orderRow in nestedRows)
                    {
                        // Lấy số order
                        var anchor = await orderRow.QuerySelectorAsync("a");
                        var orderText = anchor != null ? (await anchor.InnerTextAsync()).Trim() : string.Empty;

                        if (orderText == res.OrderNumber)
                        {
                            // Lấy nút Add tương ứng trong dòng đó
                            await Task.Delay(200);
                            var inputs = await orderRow.QuerySelectorAllAsync("input[type='text']");
                            var hasRed = false;
                            foreach (var input in inputs)
                            {
                                var styles = await input.EvaluateAsync<string[]>(@"el => {
                                        const cs = window.getComputedStyle(el);
                                        return [cs.color, cs.borderColor];
                                    }");
                                var color = styles[0];
                                var borderColor = styles[1];

                                if (color == "rgb(255, 0, 0)" || borderColor == "rgb(255, 0, 0)")
                                {
                                    Console.WriteLine("❗ Input có màu đỏ.");
                                    hasRed = true;
                                }
                            }

                            if (hasRed)
                            {
                                await row.EvaluateAsync(@"(r) => {
                                        const cb = r.querySelector('td input[type=checkbox]');
                                        if (cb && !cb.checked) {
                                            cb.click();
                                        }
                                    }");
                                string errorRow = JsonSerializer.Serialize(res);
                                removedRow.Add(errorRow);
                            }
                        }
                    }
                }
            }
        }
        if (removedRow.Count > 0)
        {
            await page.ClickAsync("#fg_delsel");
            await page.WaitForSelectorAsync("#IAIsimpleConfirm_h", new() { Timeout = 5000 });
            await page.ClickAsync("#yui-gen0-button");
        }
        //Console.ReadLine();
        await browser.CloseAsync();
        return Results.Ok(new { success = true, removedRow = removedRow });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi: " + ex.Message);
    }
});


app.MapPost("/allegro/finish-mapping", async () =>
{
    try
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage" }
        });

        IBrowserContext contextBrowser;
        if (File.Exists(storageFilePath))
        {
            contextBrowser = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storageFilePath,
                BypassCSP = true,
                RecordVideoDir = null,
                ViewportSize = null,
                BaseURL = "https://client4901.idosell.com",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            });
        }
        else
        {
            contextBrowser = await browser.NewContextAsync();
        }
        await contextBrowser.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            if (req.ResourceType == "image" || req.ResourceType == "font" || req.ResourceType == "stylesheet")
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        var page = await contextBrowser.NewPageAsync();
        var targetUrl = "https://client4901.idosell.com/panel/import-auctions.php?type=map";
        await page.GotoAsync(targetUrl);
        bool isLoginFormVisible = await page.Locator("#panel_login").IsVisibleAsync();
        if (isLoginFormVisible)
        {
            await page.FillAsync("#panel_login", "vietdao");
            await page.FillAsync("#panel_password", "Abc@12345");
            await page.ClickAsync("button[type=submit]");
            // Lưu lại storage state để dùng cho lần sau
            await contextBrowser.StorageStateAsync(new BrowserContextStorageStateOptions { Path = "storageState.json" });
            await page.GotoAsync(targetUrl);
        }
        else
        {
            Console.WriteLine("Session còn hiệu lực. Đã đăng nhập.");
        }
        await page.WaitForSelectorAsync("table.t6");
        var rows = await page.QuerySelectorAllAsync("table.t6 > tbody > tr:not(:first-child)");
        bool found = false;
        foreach (var row in rows)
        {
            var cells = await row.QuerySelectorAllAsync("td");

            if (cells.Count < 9)
                continue;
            var dateText = await cells[0].InnerTextAsync();
            if (DateTime.TryParse(dateText.Trim(), out var parsedDate))
            {
                if (parsedDate.Date < new DateTime(2025, 5, 5))
                    continue;
            }
            else
            {
                continue;
            }
            var statusText = await cells[4].InnerTextAsync();
            var waitingListingsText = await cells[5].InnerTextAsync();
            var mappedListingsText = await cells[6].InnerTextAsync();
            var errorListingsText = await cells[7].InnerTextAsync();

            int waitingListings = int.Parse(waitingListingsText.Trim());
            int mappedListings = int.Parse(mappedListingsText.Trim());
            int errorListings = int.Parse(errorListingsText.Trim());

            if ((statusText.Trim() == "Ready for mapping" || statusText.Trim() == "Gotowe do mapowania") && waitingListings > (mappedListings + errorListings))
            {
                var actionCell = cells[8];
                var viewLink = await actionCell.QuerySelectorAsync("a");
                if (viewLink != null)
                {
                    await viewLink.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    found = true;
                    break;
                }
            }
        }
        if (!found)
        {
            return Results.Ok(new { success = true });
        }
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('fg_checkAll');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");
        await page.WaitForSelectorAsync("#select_on_self", new() { Timeout = 5000 });
        await page.ClickAsync("#select_on_self");

        await page.WaitForSelectorAsync("#btnImport", new() { Timeout = 5000 });
        await page.ClickAsync("#btnImport");

        await page.SelectOptionAsync("#fg_preset", new SelectOptionValue { Label = "royal_fashion" });
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('reimport_auction_parameters');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('generate_responsible_entities');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('reimport_auction_variant_offers');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('reimport_productization');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");
        await page.EvaluateAsync(@"() => {
            const cb = document.getElementById('generate_shop_params');
            if (cb && !cb.checked) {
                cb.click();
            }
        }");

        await page.ClickAsync("#choice_import_auctions_toplayer");

        await Task.WhenAny(
            page.Locator("#info_toplayer_h").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 }),
            page.Locator("#err_toplayer_h").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 })
        );
        while (true)
        {
            bool infoVisible = await page.Locator("#info_toplayer_h").IsVisibleAsync();
            bool errorVisible = await page.Locator("#err_toplayer_h").IsVisibleAsync();

            if (!infoVisible && !errorVisible)
            {
                Debug.WriteLine("khong hien thi nua");
                break; // Không còn popup nào nữa, thoát vòng lặp
            }

            if (infoVisible)
            {
                Debug.WriteLine("infor");

                var checkboxInfo = page.Locator("#jsfg_ignore_0");
                await checkboxInfo.WaitForAsync(new() { State = WaitForSelectorState.Visible });
                await page.EvaluateAsync(@"() => {
                    const cb = document.getElementById('jsfg_ignore_0');
                    if (cb && !cb.checked) {
                        cb.click();
                    }
                }");
                await page.ClickAsync("#btnInfo");
                await page.Locator("#info_toplayer_h").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            }

            if (errorVisible)
            {
                Debug.WriteLine("error");

                var checkboxError = page.Locator("#jsfg_ignoreError_0");
                await checkboxError.WaitForAsync(new() { State = WaitForSelectorState.Visible });
                await page.EvaluateAsync(@"() => {
                    const cb = document.getElementById('jsfg_ignoreError_0');
                    if (cb && !cb.checked) {
                        cb.click();
                    }
                }");
                await page.ClickAsync("#btnError");
                await page.Locator("#err_toplayer_h").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            }
        }
        await page.Locator("#import_wait-content").WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 60000
        });

        await browser.CloseAsync();
        // Console.WriteLine("Script chạy xong. Nhấn Enter để đóng...");
        // Console.ReadLine(); // giữ thread mở
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi: " + ex.Message);
    }
});

app.MapPost("/allegro/mapping", async (HttpRequest request) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(); // Lấy chuỗi string từ request body

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage" }
        });

        IBrowserContext contextBrowser;
        if (File.Exists(storageFilePath))
        {
            contextBrowser = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storageFilePath,
                BypassCSP = true,
                RecordVideoDir = null,
                ViewportSize = null,
                BaseURL = "https://client4901.idosell.com",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
            });
        }
        else
        {
            contextBrowser = await browser.NewContextAsync();
        }
        await contextBrowser.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            if (req.ResourceType == "image" || req.ResourceType == "font" || req.ResourceType == "stylesheet")
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        var page = await contextBrowser.NewPageAsync();
        var targetUrl = "https://client4901.idosell.com/panel/import-auctions.php?type=map";
        await page.GotoAsync(targetUrl);
        bool isLoginFormVisible = await page.Locator("#panel_login").IsVisibleAsync();
        if (isLoginFormVisible)
        {
            await page.FillAsync("#panel_login", "vietdao");
            await page.FillAsync("#panel_password", "Abc@12345");
            await page.ClickAsync("button[type=submit]");
            // Lưu lại storage state để dùng cho lần sau
            await contextBrowser.StorageStateAsync(new BrowserContextStorageStateOptions { Path = "storageState.json" });
            await page.GotoAsync(targetUrl);
        }
        else
        {
            Console.WriteLine("Session còn hiệu lực. Đã đăng nhập.");
        }

        var buttonNewMapping = page.ClickAsync("#btnNewImport");
        await page.WaitForSelectorAsync("#jsfg_importType_1", new() { State = WaitForSelectorState.Visible });
        var checkbox = await page.QuerySelectorAsync("#jsfg_importType_1");
        if (checkbox != null)
        {
            bool isChecked = await checkbox.IsCheckedAsync();
            if (!isChecked)
            {
                await checkbox.CheckAsync();
            }
        }
        else
        {
            Console.WriteLine("Không tìm thấy checkbox #jsfg_importType_1.");
        }
        await page.ClickAsync("#choice_import_toplayer");
        await page.WaitForSelectorAsync("#new_import_toplayer_h", new() { State = WaitForSelectorState.Visible });

        await page.FillAsync("#fg_textImport", payload);

        await page.ClickAsync("#btnPrepareImport");

        await browser.CloseAsync();
        //Console.WriteLine("Script chạy xong. Nhấn Enter để đóng...");
        //Console.ReadLine(); // giữ thread mở
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi: " + ex.Message);
    }
});


app.Run();
