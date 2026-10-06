using TokNotch.Core.Models;
namespace TokNotch.Infrastructure.Usage;
/// <summary>
/// DSH always rings the DeepSeek API balance, reusing the single balance query and cache of the DeepSeek row.
/// This is an explicit association, not proof that every DSH model bills that account, so the label says so.
/// </summary>
public static class DshBalanceBinding
{
 public const string Association="DSH 余额关联：DeepSeek API";
 public static ProviderUsageSnapshot Attach(ProviderUsageSnapshot local,ProviderUsageSnapshot deepSeek)
 {
  var detail=deepSeek.BalanceStatus??(deepSeek.Balance is null?"DeepSeek 余额未连接 · 设置 → DeepSeek API":"DeepSeek 余额已连接");
  return local with {Balance=deepSeek.Balance,Currency=deepSeek.Currency,BalanceSource="DeepSeek",BalanceStatus=$"{Association} · {detail}"};
 }
}
