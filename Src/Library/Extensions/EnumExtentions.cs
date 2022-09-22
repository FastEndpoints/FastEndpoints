namespace FastEndpoints;
internal static class EnumExtentions
{
    /// <summary>
    /// Split an enum into its individual flags
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="e"></param>
    /// <returns></returns>
    public static IList<T> SplitEnum<T>(this T e) where T : Enum
    {
        if (e is null)
            return new List<T>();

        var result = new List<T>();
        foreach (T item in Enum.GetValues(typeof(T)))
        {
            if ((Convert.ToInt32(item) & Convert.ToInt32(e)) > 0)
            {
                result.Add(item);
            }
        }
        return result;
    }
}