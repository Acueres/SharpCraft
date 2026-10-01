namespace SharpCraft.Rendering;

internal readonly record struct DetailedReportLayout(int Columns, int Rows, int Headers,
    float Left, float Top, float Width, float ChartTop, float ChartHeight, float BodyTop, float FooterTop)
{
    public static DetailedReportLayout Calculate(float width, float height, float padding,
        float characterWidth, float lineHeight, int maximumRows)
    {
        float left = padding * 2, top = padding * 2;
        float contentWidth = Math.Max(1, width - padding * 4);
        float footerTop = Math.Max(top, height - padding * 2 - lineHeight);
        int headers = Math.Clamp((int)((footerTop - top) / lineHeight), 0, 4);
        float chartTop = top + headers * lineHeight;
        float chartHeight = headers == 4 && footerTop - chartTop >= 60 + lineHeight * 2 ? 48 : 0;
        float bodyTop = chartTop + (chartHeight > 0 ? 60 : 0);
        int rows = Math.Clamp((int)((footerTop - lineHeight * .5f - bodyTop) / lineHeight), 0, maximumRows);
        return new(Math.Max(1, (int)(contentWidth / characterWidth)), rows, headers,
            left, top, contentWidth, chartTop, chartHeight, bodyTop, footerTop);
    }
}
