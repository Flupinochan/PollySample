using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.DependencyInjection;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Text.Json;

namespace PollySample;
/// <summary>
/// PollyのDI設定
/// </summary>
public class PollyConfig
{
    public static void Configure(IServiceCollection services)
    {
        // Pipelineの作成
        // ※RetryやTimeoutをAddする順番は重要
        // 1. Fallback
        // 2. Outer Timeout
        // 3. Retry
        // 4. Inner Timeout
        services.AddResiliencePipeline<String, HttpResponseMessage>("my-pipeline", static (builder, context) =>
        {
            PollyPipeline(builder, context);
        });
    }

    /// <summary>
    /// 匿名関数内で定義すると、匿名クラスにコンパイルされて、ファイル名や関数名などがログ出力されないため注意
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="context"></param>
    private static void PollyPipeline(ResiliencePipelineBuilder<HttpResponseMessage> builder, AddResiliencePipelineContext<String> context)
    {
        ILogger<PollyConfig> logger = context.ServiceProvider.GetRequiredService<ILogger<PollyConfig>>();
        JsonSerializerOptions jsonSerializerOptions = context.ServiceProvider.GetRequiredService<JsonSerializerOptions>();

        // 1. 処理全体(最終結果)を処理
        //    例外が発生もしくはResponseがnullだった場合に、仮のResponseを設定
        builder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = (FallbackPredicateArguments<HttpResponseMessage> fallbackPredicateArguments) =>
            {
                // ※HttpStatusCodeではなく、独自のステータスコードを用意し、
                // switch文で、後続の処理は独自のステータスコードに応じて処理をする
                if(fallbackPredicateArguments.Outcome.Exception is not null || fallbackPredicateArguments.Outcome.Result is null)
                    return PredicateResult.True();
                else
                    return PredicateResult.False();
            },
            OnFallback = (OnFallbackArguments<HttpResponseMessage> OnFallbackArguments) =>
            {
                // FallbackAction実行前の処理
                logger.LogDebug("Fallback処理を実行します");
                return default;
            },
            FallbackAction = (FallbackActionArguments<HttpResponseMessage> fallbackActionArguments) =>
            {
                MyResponseModel myResponseModel = new MyResponseModel();
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage()
                {
                    Content = new StringContent(JsonSerializer.Serialize(myResponseModel, jsonSerializerOptions))
                };
                return Outcome.FromResultAsValueTask(httpResponseMessage);
            }
        })
        // 処理全体のTimeout
        .AddTimeout(new TimeoutStrategyOptions
        {
            //Timeout = TimeSpan.FromSeconds(5),
            Timeout = TimeSpan.FromSeconds(60),
            OnTimeout = (OnTimeoutArguments onTimeoutArguments) =>
            {
                logger.LogError("処理全体でTimeoutが発生しました");
                return default;
            }
        })
        // Retry
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            /// DelayBackoffType.Linear: リトライごとにDelayが倍増していく 3, 6, 9...
            /// UseJitter: Delayが25%増減する
            /// MaxDelay: Linearなどで増加したDelayの最大Delay
            /// Delay: リトライまでの待機時間
            BackoffType = DelayBackoffType.Linear,
            UseJitter = true,
            MaxDelay = TimeSpan.FromSeconds(10),
            Delay = TimeSpan.FromSeconds(3),
            MaxRetryAttempts = 3,
            ShouldHandle = (RetryPredicateArguments<HttpResponseMessage> retryPredicateArguments) =>
            {
                if(retryPredicateArguments.Outcome.Result is null)
                {
                    logger.LogError($"{retryPredicateArguments.Outcome.Exception!.GetType().Name}: 通信エラー(Timeout or Exception)が発生しました");
                    return PredicateResult.True();
                }
                else
                {
                    HttpResponseMessage response = retryPredicateArguments.Outcome.Result;
                    if(response.StatusCode != HttpStatusCode.OK)
                    {
                        logger.LogError($"{response.StatusCode}: サーバから異常な応答が返ってきました");
                        return PredicateResult.True();
                    }
                    else
                    {
                        logger.LogInformation($"{response.StatusCode}: サーバから正常な応答が返ってきました");
                        return PredicateResult.False();
                    }
                }
            },
            OnRetry = (OnRetryArguments<HttpResponseMessage> onRetryArguments) =>
            {
                logger.LogDebug($"{onRetryArguments.AttemptNumber + 1}回目のリトライを{onRetryArguments.RetryDelay}秒後に実行します");
                return default;
            }
        })
        // リトライごとに適用されるTimeout
        .AddTimeout(new TimeoutStrategyOptions
        {
            //Timeout = TimeSpan.FromMilliseconds(10),
            Timeout = TimeSpan.FromSeconds(5),
            OnTimeout = (OnTimeoutArguments onTimeoutArguments) =>
            {
                logger.LogError("リクエスト(Retry)でTimeoutが発生しました");
                return default;
            }
        });
        // .AddFallback(xxx) // 必要であれば、リトライ(各処理)ごとに適用されるFallbackを設定

        // Timeoutメモ
        /// Timeout専用のCancellationTokenを内部(inner)に所有している
        /// Timeoutが発生した場合は処理をキャンセルし、例外(TimeoutRejectedException)をスローする
        /// try-catchしてもいいし、fallback処理してもよい
        /// 
        /// outerCancelTokenを利用する場合、すでにキャンセルされているouterCancelTokenを渡したら、
        /// Fallback処理されず、即座に例外(OperationCanceledException)が発生するので気を付けること!
    }
}
