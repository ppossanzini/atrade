namespace AutoTrade.Trading.Core.Dto
{
  /// <summary>
  /// Request body for importing a token pair obtained outside the consent flow, as issued by the
  /// official Playground. Secrets travel only in the request body and are never echoed back.
  /// </summary>
  public class BrokerTokenImportDto
  {
    public string AccessToken { get; set; }

    public string RefreshToken { get; set; }
  }
}
