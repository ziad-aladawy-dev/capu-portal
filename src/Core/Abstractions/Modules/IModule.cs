using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Abstractions.Modules;

public interface IModule
{
    void Register(IServiceCollection services);
}
