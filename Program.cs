using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


var UserInfo = new List <User>
{
   new User { Id = 1, Name = "Alice", Email = "alice@example.com", Department = "Engineering" },
   new User { Id = 2, Name = "Bob", Email = "bob@example.com", Department = "Marketing" }
};
//error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next(); // continue down the pipeline
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "An unexpected error occurred.",
            details = ex.Message
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(errorResponse));
    }
});
// Authentication middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Check for Authorization header
    if (!context.Request.Headers.TryGetValue("Authorization", out var token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "Unauthorized",
            details = "Missing Authorization header"
        }));
        return;
    }

    
    var validToken = "HelloWorld123"; // In real applications, use a secure method to store and validate tokens
    if (token != $"Bearer {validToken}")
    {
        logger.LogWarning("Invalid token received: {token}", token);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "Unauthorized",
            details = "Invalid token"
        }));
        return;
    }

    // Token is valid → continue
    await next();
});

// logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Log request details
    logger.LogInformation("Incoming Request: {method} {url}", context.Request.Method, context.Request.Path);

    // Capture the original response body
    var originalBodyStream = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    // Continue down the pipeline
    await next();

    // Read response
    context.Response.Body.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(context.Response.Body).ReadToEndAsync();
    context.Response.Body.Seek(0, SeekOrigin.Begin);

    // Log response details
    logger.LogInformation("Response: {statusCode} {body}", context.Response.StatusCode, responseText);

    // Copy back to original stream
    await responseBody.CopyToAsync(originalBodyStream);
});


app.MapGet("/", () => "I am Root!");

app.MapGet("/user", () => UserInfo);

app.MapGet("/user/{id}",(int id)=>
{
    var user = UserInfo.FirstOrDefault(u => u.Id == id);
    if (user != null)
    {
        return Results.Ok(user);
    }
    else
    {
        return Results.NotFound("User not found!");
    }
});

app.MapPost("/user",(User user)=>
{

    
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(user);
        if (!Validator.TryValidateObject(user, context, validationResults, true))
        {
            return Results.BadRequest(new { Errors = validationResults.Select(v => v.ErrorMessage) });
        }

        int newId = UserInfo.Select(u => u.Id).DefaultIfEmpty(0).Max() + 1;
        user.Id = newId;
        UserInfo.Add(user);
        return Results.Created($"/user/{newId}", user);
    
   
});

app.MapPut("/user/{id}",(int id, User user)=>
{
    

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(user);
        if (!Validator.TryValidateObject(user, context, validationResults, true))
        {
            return Results.BadRequest(new { Errors = validationResults.Select(v => v.ErrorMessage) });
        }

        var existingUser = UserInfo.FirstOrDefault(u => u.Id == id);
        if (existingUser == null)
            return Results.NotFound(new { Message = $"User with id {id} not found." });

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;
        existingUser.Department = user.Department;
        return Results.Ok(existingUser);
    
   
});

app.MapDelete("/user/{id}",(int id)=>
{
    var user = UserInfo.FirstOrDefault(u => u.Id == id);
    if (user != null)
    {
        UserInfo.Remove(user);
        return Results.Ok($"User with id {id} deleted.");
    }
    else
    {
        return Results.NotFound("User not found!");
    }
});

app.Run();

public class User
{
    public int Id { get; set; } // Auto-generated

    [Required(ErrorMessage = "Name is required")]
    [StringLength(15, ErrorMessage = "Name must be under 15 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required")]
    public string Department { get; set; } = string.Empty;
}
