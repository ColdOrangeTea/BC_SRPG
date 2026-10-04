namespace BlackChess.SRPG.Units
{
    /// <summary>
    /// 統一的「血量影響對象」判定。
    /// 需求：人物的攻擊、未來新增的技能、道具的影響……任何會對血量產生影響的事物，
    /// 都要有「是否會影響同陣營夥伴」的設定 —— 全部集中到這裡判定，規則才會一致：
    ///   • 有害效果 (傷害/減益)：敵對陣營一定會被影響；同陣營只有在 affectsAllies=true 時才波及
    ///     (例如無差別大範圍攻擊、炸藥)。來源勢力為 null 視為「中立」(例如場上的炸藥物件)，無差別影響所有人。
    ///   • 有益效果 (治療/增益)：使用者自己一定可以；其他人限「同陣營 且 affectsAllies=true」。
    /// </summary>
    public static class HitPolicy
    {
        /// <summary>有害效果 (傷害/減益) 是否會影響 target。sourceFaction = null 視為中立來源 (無差別)。</summary>
        public static bool HarmAffects(Faction sourceFaction, Unit target, bool affectsAllies)
        {
            if (target == null || !target.IsAlive) return false;
            if (sourceFaction == null || target.faction == null) return true;   // 中立來源 / 無陣營 → 無差別
            if (sourceFaction.IsHostileTo(target.faction)) return true;         // 敵對陣營一定會被影響
            return affectsAllies;                                               // 同陣營 → 取決於設定
        }

        /// <summary>有益效果 (治療/增益) 是否會影響 target。</summary>
        public static bool BenefitAffects(Unit source, Unit target, bool affectsAllies)
        {
            if (source == null || target == null || !target.IsAlive) return false;
            if (target == source) return true;                                  // 自己一定可以
            return affectsAllies && source.IsFriendlyTo(target);                // 其他人限同陣營且開啟
        }
    }
}
