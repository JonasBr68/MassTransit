namespace MassTransit.Definition
{
    using System.Collections.Generic;
    using System.Linq;


    public static class EndpointDefinitionExtensions
    {
        public static IEndpointDefinition Combine(this IEnumerable<IEndpointDefinition> definitions)
        {
            var list = definitions.ToList();
            if (list.Count == 0)
                return default;

            if (list.Count == 1)
                return list[0];

            return new CombinedEndpointDefinition(list);
        }
    }
}
