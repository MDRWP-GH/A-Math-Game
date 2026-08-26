using System.Text;

namespace AMath.Gameplay.Board
{
    /// <summary>Formats validation + score breakdown for match HUD / teaching UI.</summary>
    public static class PlacementPreviewFormatter
    {
        /// <summary>
        /// Language selector injected by the UI layer (true = Thai). Defaults to
        /// Thai when nothing is wired, matching the original behaviour, so the
        /// gameplay assembly never has to know about the settings system.
        /// </summary>
        public static System.Func<bool> ThaiSelector { get; set; }

        private static bool Thai => ThaiSelector?.Invoke() ?? true;

        /// <summary>Maps engine validation errors to short player-facing text.</summary>
        public static string LocalizeError(string error)
        {
            bool thai = Thai;
            if (string.IsNullOrWhiteSpace(error))
                return thai ? "การวางไม่ถูกต้อง" : "Invalid placement";

            if (error.Contains("center", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ตาแรกต้องวางสมการให้ผ่านช่องกลางกระดาน"
                    : "The first equation must cover the center square";
            if (error.Contains("connect", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ไทล์ใหม่ต้องต่อกับไทล์ที่มีอยู่บนกระดาน"
                    : "New tiles must connect to tiles already on the board";
            if (error.Contains("contiguous", System.StringComparison.OrdinalIgnoreCase)
                || error.Contains("single row", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ต้องวางในแถวหรือคอลัมน์เดียว และต่อเนื่องไม่มีช่องว่าง"
                    : "Tiles must sit in one row or column with no gaps";
            if (error.Contains("not equal", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "สมการไม่ถูกต้อง: ทั้งสองข้างของ '=' ไม่เท่ากัน"
                    : "Invalid equation: both sides of '=' are not equal";
            if (error.Contains("too short", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "สมการสั้นเกินไป (อย่างน้อย 3 ชิ้น)"
                    : "Equation is too short (at least 3 tiles)";
            if (error.Contains("must contain '='", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "สมการต้องมีเครื่องหมาย '='"
                    : "An equation must contain '='";
            if (error.Contains("own those tiles", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ใช้ไทล์ที่ไม่มีในมือ"
                    : "You do not own those tiles";
            if (error.Contains("declaration", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ไทล์ยืดหยุ่นต้องเลือกค่าก่อนวาง"
                    : "Flexible tiles must be declared before placing";
            if (error.Contains("already occupied", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "ช่องนี้มีไทล์อยู่แล้ว"
                    : "That cell is already occupied";
            if (error.Contains("outside", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "วางนอกกระดาน"
                    : "Placement is outside the board";
            if (error.Contains("does not form an equation", System.StringComparison.OrdinalIgnoreCase))
                return thai
                    ? "การวางนี้ยังไม่เกิดสมการ"
                    : "This placement does not form an equation yet";

            return error;
        }

        /// <summary>Multi-line preview: validity, equations, per-tile points, total.</summary>
        public static string FormatPreview(PlacementValidation validation, PlacementScoreBreakdown breakdown)
        {
            bool thai = Thai;
            if (validation == null || !validation.IsValid)
                return "✗ " + LocalizeError(validation?.Error);

            string points = thai ? " แต้ม" : " pts";
            var text = new StringBuilder(256);
            text.AppendLine(thai ? "✓ สมการถูกต้อง" : "✓ Valid equation");

            if (breakdown != null)
            {
                for (int i = 0; i < breakdown.Equations.Count; i++)
                {
                    EquationScoreLine eq = breakdown.Equations[i];
                    text.Append("  ").Append(eq.EquationText);
                    if (eq.EquationMultiplier > 1)
                        text.Append(" ×").Append(eq.EquationMultiplier);
                    text.Append(" = ").Append(eq.LineSubtotal).AppendLine(points);

                    for (int t = 0; t < eq.Tiles.Count; t++)
                    {
                        TileScoreContribution tile = eq.Tiles[t];
                        if (!tile.IsNew) continue;
                        text.Append("    ")
                            .Append(AMathTileSet.SymbolOf(tile.EffectiveTileId))
                            .Append(" @(").Append(tile.X).Append(',').Append(tile.Y).Append(") ")
                            .Append(tile.FacePoints).Append(points);
                        if (tile.TilePremium > 1)
                            text.Append(" ×").Append(tile.TilePremium)
                                .Append(thai ? " (ช่องคูณไทล์)" : " (tile multiplier)");
                        if (tile.CellPremium == PremiumType.EquationX2 || tile.CellPremium == PremiumType.EquationX3)
                            text.Append(thai ? " [ช่องคูณสมการ]" : " [equation multiplier]");
                        text.AppendLine();
                    }
                }

                if (breakdown.FullRackBonus > 0)
                    text.Append(thai ? "  โบนัสใช้ครบมือ: +" : "  Full-rack bonus: +")
                        .Append(breakdown.FullRackBonus).AppendLine();

                text.Append(thai ? "รวม " : "Total ").Append(breakdown.Total).Append(points);
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>Empty-cell premium hint for the board HUD.</summary>
        public static string PremiumHint(PremiumType premium) => premium switch
        {
            PremiumType.TileX2 => "x2\nPIECE",
            PremiumType.TileX3 => "x3\nPIECE",
            PremiumType.EquationX2 => "x2\nWORD",
            PremiumType.EquationX3 => "x3\nWORD",
            _ => string.Empty
        };
    }
}
