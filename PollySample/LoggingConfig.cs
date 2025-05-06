using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PollySample;

/// <summary>
/// LoggerのDI設定
/// </summary>
public class LoggingConfig
{
    public static void Configure(IServiceCollection services)
    {
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "yyyy/MM/dd HH:mm:ss ";
                options.SingleLine = true;
            });
            loggingBuilder.AddDebug();
            loggingBuilder.AddFilter((category, logLevel) =>
            {
                if(category is null)
                    return false;
                return category.Contains("PollySample");
            });
        });
    }
}
