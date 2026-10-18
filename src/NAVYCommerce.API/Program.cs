using NAVYCommerce.API.Extensions.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNavyPersistence(builder.Configuration);
builder.Services.AddNavyApi(builder.Configuration, builder.Environment);
builder.Services.AddNavyCommerce(builder.Configuration);
builder.Services.AddNavyProductMedia(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

if (app.Configuration.GetValue<bool>("HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.Run();
