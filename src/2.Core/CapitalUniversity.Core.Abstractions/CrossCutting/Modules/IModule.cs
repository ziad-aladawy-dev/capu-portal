using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Modules;

public interface IModule
{
    void Register(IServiceCollection services);
}
