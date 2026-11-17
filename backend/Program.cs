using backend.BackgroundJobs;
using backend.Data;
using backend.Middleware;
using backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

namespace backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsql =>
                {
                    npgsql.UseNetTopologySuite();
                    npgsql.CommandTimeout(30);
                });
                if (builder.Environment.IsDevelopment())
                {
                    options.EnableDetailedErrors();
                }
            });
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IBranchSearchService, BranchSearchService>();
            builder.Services.AddScoped<IBusinessService, BusinessService>();
            builder.Services.AddHostedService<ArchiveEventsJob>();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            builder.Services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
            });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers(options =>
            {
                options.ReturnHttpNotAcceptable = true;
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .WithExposedHeaders("X-Created-Resource-Id", "Location", "ETag"));
            });
            builder.Services.AddOpenApi();

            WebApplication app = builder.Build();
            app.UseExceptionHandler();
            app.UseStatusCodePages();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }
            app.UseCors("CorsPolicy");
            //app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
