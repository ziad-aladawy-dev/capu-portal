using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Modules;

public interface IModule
{
    void Register(IServiceCollection services);
}
