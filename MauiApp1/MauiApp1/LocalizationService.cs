using System.Globalization;
using MauiApp1.Resources.Strings;

namespace MauiApp1
{
    public static class LocalizationService
    {
        public static CultureInfo CurrentCulture => AppResources.Culture ?? CultureInfo.CurrentUICulture;

        public static void SetCulture(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            AppResources.Culture = culture;
        }
    }
}
