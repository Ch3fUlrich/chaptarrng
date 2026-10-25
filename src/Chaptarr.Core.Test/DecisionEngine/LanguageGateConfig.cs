using System.Reflection;
using NzbDrone.Core.Configuration;

namespace Chaptarr.Core.Test.DecisionEngine
{
    // Minimal IConfigService for the language gate: only AllowUnknownReleaseLanguage is configurable.
    public class LanguageGateConfig : DispatchProxy
    {
        private bool _allowUnknown;

        public static IConfigService Create(bool allowUnknown)
        {
            var proxy = DispatchProxy.Create<IConfigService, LanguageGateConfig>();
            ((LanguageGateConfig)(object)proxy)._allowUnknown = allowUnknown;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == "get_" + nameof(IConfigService.AllowUnknownReleaseLanguage))
            {
                return _allowUnknown;
            }

            var type = targetMethod.ReturnType;
            return type.IsValueType && type != typeof(void) ? System.Activator.CreateInstance(type) : null;
        }
    }
}
