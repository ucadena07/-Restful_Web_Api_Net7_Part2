using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;

namespace MagicVilla_VillaAPI.Extensions
{
    public static class CustomExceptionExtensions
    {
        public static void HandleError(this IApplicationBuilder app, bool IsDevelopment)
        {
            app.UseExceptionHandler(err =>
            {
                err.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    var feature = context.Features.Get<IExceptionHandlerFeature>();
                    if (feature != null)
                    {
                        if (IsDevelopment)
                        {
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                            {
                                StatusCode = context.Response.StatusCode,
                                ErrorMessage = feature.Error.Message,
                                StackTrace = feature.Error.StackTrace
                            }));
                        }
                        else
                        {
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                            {
                                StatusCode = context.Response.StatusCode,
                                ErrorMessage = "Hello from program.cs"
                            }));
                        }

                    }

                });
            });
        }
    }
}
