using System;
using System.Globalization;
using System.Windows.Markup;
using it.carpanese.utilities.MDViewer.Properties;

namespace it.carpanese.utilities.MDViewer
{
    public static class Localizer
    {
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var text = Resources.ResourceManager.GetString(key, Resources.Culture);
            return string.IsNullOrEmpty(text) ? key : text;
        }

        public static string Format(string key, params object[] args)
        {
            var format = Get(key);
            return args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.CurrentCulture, format, args);
        }
    }

    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Localizer.Get(Key);
        }
    }
}
