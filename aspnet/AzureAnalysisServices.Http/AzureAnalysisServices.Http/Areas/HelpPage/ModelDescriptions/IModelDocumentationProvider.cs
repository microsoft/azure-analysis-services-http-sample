using System;
using System.Reflection;

namespace Microsoft.Samples.AzureAnalysisServices.Http.Areas.HelpPage.ModelDescriptions
{
    public interface IModelDocumentationProvider
    {
        string GetDocumentation(MemberInfo member);

        string GetDocumentation(Type type);
    }
}