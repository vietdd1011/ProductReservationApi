using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using ProductReservationApi.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;


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
        bool isLoginFormVisible = await page.Locator("#panel_login").IsVisibleAsync();
        var targetUrl = "https://client4901.idosell.com/panel/stocks-dislocate.php?action=edit&document_id=" + docId;
        await page.GotoAsync(targetUrl);
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
                            var img = await orderRow.QuerySelectorAsync("img[alt='Add to invoice']");
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

        await page.WaitForSelectorAsync("#fg_save_order", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
        await page.ClickAsync("#fg_save_order");

        await browser.CloseAsync();
        return Results.Ok(new { success = true, count = reservations.Count });
    }
    catch (Exception ex)
    {
        return Results.Problem("Lỗi: " + ex.Message);
    }
});


app.Run();
