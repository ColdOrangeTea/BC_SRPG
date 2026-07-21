using BlackChess.SRPG.Units;

namespace BlackChess.SRPG.Actions
{
    /// <summary>
    /// 「待機」行動。需求書規定：待機自動視為防禦。
    /// 執行後單位本回合結束 (HasMoved / HasActed 都設 true)，並進入防禦狀態 (受傷減半)。
    /// </summary>
    public static class WaitAction
    {
        public static void Execute(Unit unit)
        {
            if (unit == null) return;
            unit.Defend(); // 內部同時設定 IsDefending 與結束回合旗標
        }
    }
}
