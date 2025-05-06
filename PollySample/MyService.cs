using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace PollySample;

/// <summary>
/// リクエストの実行
/// </summary>
/// <param name="httpClient"></param>
/// <param name="pollyPipeline"></param>
public class MyService(
    HttpClient httpClient,
    [FromKeyedServices("my-pipeline")]
    ResiliencePipeline<HttpResponseMessage> pollyPipeline
    )
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="requestUrl">リクエストするURL</param>
    /// <param name="outerCancellationToken">リクエスト処理キャンセル用トークン</param>
    /// <returns></returns>
    public async Task<HttpResponseMessage> GetRequestAndPrint(String requestUrl, CancellationToken outerCancellationToken)
    {
        HttpResponseMessage httpResponseMessage = await pollyPipeline.ExecuteAsync(async (CancellationToken innerCancellationToken) =>
        {
            return await httpClient.GetAsync(requestUrl, innerCancellationToken);
        }, outerCancellationToken);

        return httpResponseMessage;
    }
}