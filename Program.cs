using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/home", async () =>
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
            Headless = true // Để headless khi chạy server
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Truy cập panel
        await page.GotoAsync("https://client4901.idosell.com");

        // Đăng nhập nếu cần
        await page.FillAsync("#panel_login", "vietdao");
        await page.FillAsync("#panel_password", "Abc@12345");
        await page.ClickAsync("button[type=submit]");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Truy cập trang danh sách sản phẩm đang được order
        await page.GotoAsync("https://client4901.idosell.com/panel/product-aside.php?action=view&mode=3&stock=2&order_stock=5");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Lấy dữ liệu từ bảng
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

                    // Quantity
                    var quantity = Regex.Match(text, @"-\s*(\d+)\s+para").Groups[1].Value;

                    // Order Type
                    var orderType = text.ToLower().Contains("shein") ? "shein" : "royal";

                    if (!string.IsNullOrEmpty(productCode))
                    {
                        data.Add(new { productCode, quantity, orderType });
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

app.Run();
