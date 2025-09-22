using System.Linq;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AnchorSafe.API.Compatibility
{
    /// <summary>
    /// Adds attribute route templates for legacy <see cref="ApiController"/> actions so they appear in Swagger.
    /// </summary>
    public sealed class LegacyApiExplorerConvention : IApplicationModelConvention
    {
        /// <inheritdoc />
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                if (!typeof(ApiController).IsAssignableFrom(controller.ControllerType))
                {
                    continue;
                }

                var hasVisibleAction = false;

                foreach (var action in controller.Actions)
                {
                    var hasHttpMethod = action.Selectors.Any(selector =>
                        selector.ActionConstraints?.OfType<HttpMethodActionConstraint>().Any() == true);

                    if (!hasHttpMethod)
                    {
                        action.ApiExplorer.IsVisible = false;
                        continue;
                    }

                    action.ApiExplorer.IsVisible = true;
                    hasVisibleAction = true;

                    if (action.Selectors.Any(selector => selector.AttributeRouteModel != null))
                    {
                        continue;
                    }

                    var template = $"{controller.ControllerName}/{action.ActionName}";
                    var attributeRoute = new AttributeRouteModel(new RouteAttribute(template));

                    foreach (var selector in action.Selectors)
                    {
                        selector.AttributeRouteModel = attributeRoute;
                    }
                }

                controller.ApiExplorer.IsVisible = hasVisibleAction;
            }
        }
    }
}
