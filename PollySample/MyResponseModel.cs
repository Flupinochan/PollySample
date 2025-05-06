namespace PollySample;

/// <summary>
/// レスポンスボディの定義
/// </summary>
public class MyResponseModel
{
    public Int32 StatusCode { get; set; } = 1003;
    public String Message { get; set; } = "Error: Fallback処理で生成されたMessageです";

    public MyResponseModel() { }
}
