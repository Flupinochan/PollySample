using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PollySample;

partial class Program
{
    public static IServiceProvider Services { get; private set; }
    private static readonly ILogger<Program> _logger;
    private static readonly MyService _myService;
    private static readonly JsonSerializerOptions _jsonSerializerOptions;

    static Program()
    {
        Services = ConfigureServices();
        _logger = Services.GetRequiredService<ILogger<Program>>();
        _myService = Services.GetRequiredService<MyService>();
        _jsonSerializerOptions = Services.GetRequiredService<JsonSerializerOptions>();

    }

    /// <summary>
    /// Dependency Injection設定
    /// </summary>
    /// <returns></returns>
    private static IServiceProvider ConfigureServices()
    {
        ServiceCollection services = new ServiceCollection();
        LoggingConfig.Configure(services);
        services.AddSingleton(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        PollyConfig.Configure(services);
        services.AddHttpClient<MyService>().ConfigureHttpClient(client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddSingleton<MyService>();
        IServiceProvider Services = services.BuildServiceProvider();
        return Services;
    }

    /// <summary>
    /// エントリーポイント
    /// </summary>
    /// <returns></returns>
    static async Task Main()
    {
        _logger.LogInformation("メイン処理開始");
        //String requestUrl = "https://xb3ztbjdfy7fwxpujbfodwjqlq0aiywl.lambda-url.ap-northeast-1.on.aws/";
        String requestUrl = "https://abcde.fgh/";
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CancellationToken outerCancellationToken = cancellationTokenSource.Token;
        _ = MonitorEnterKeyPress(cancellationTokenSource);

        // バッチ処理等で複数回リクエストする場合を想定
        Boolean exitLoop = false;
        for(Int32 i = 1; i <= 3; i++)
        {
            if(exitLoop)
                break;

            if(!outerCancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation($"{i}回目のリクエスト処理開始");
                HttpResponseMessage httpResponseMessage = await _myService.GetRequestAndPrint(requestUrl, outerCancellationToken);
                _logger.LogInformation($"{i}回目のリクエスト処理終了");

                _logger.LogInformation($"{i}回目のレスポンス処理開始");
                String content = await httpResponseMessage.Content.ReadAsStringAsync();
                MyResponseModel myResponseModel = JsonSerializer.Deserialize<MyResponseModel>(content, _jsonSerializerOptions)!;
                switch(myResponseModel.StatusCode)
                {
                    // 正常なレスポンスの場合
                    case 1001:
                        _logger.LogInformation("サーバから正常な応答が得られました");
                        _logger.LogInformation($"{myResponseModel.StatusCode}: {myResponseModel.Message}");
                        break;
                    // 異常なレスポンスの場合
                    case 1002:
                        _logger.LogError("サーバから異常な応答が得られました");
                        _logger.LogError($"{myResponseModel.StatusCode}: {myResponseModel.Message}");
                        break;
                    // 通信エラーでPollyでレスポンスを置き換えた場合
                    case 1003:
                        _logger.LogError("サーバとの通信に失敗しました");
                        _logger.LogError($"{myResponseModel.StatusCode}: {myResponseModel.Message}");
                        // 通信エラー時は後続の処理はしない
                        exitLoop = true;
                        break;
                }
                _logger.LogInformation($"{i}回目のレスポンス処理終了");
            }
        }

        _logger.LogInformation("メイン処理終了");
    }

    /// <summary>
    /// Enterキーがおされたらリクエスト処理をキャンセルする
    /// </summary>
    /// <param name="cts"></param>
    /// <returns></returns>
    static async Task MonitorEnterKeyPress(CancellationTokenSource cts)
    {
        await Task.Run(() =>
        {
            Console.WriteLine("Enter キーを押すとリクエスト処理をキャンセルします...");
            while(true)
            {
                if(Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                {
                    cts.Cancel();
                    _logger.LogWarning("リクエスト処理をキャンセルしました");
                    break;
                }
            }
        });
    }
}