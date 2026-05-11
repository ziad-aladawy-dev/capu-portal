using CapitalUniversity.Core.Domain.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Application.UniversityStructure;

public class UniversityStructureService : IUniversityStructureService
{
    private readonly IStructureNodeRepository _repository;

    public UniversityStructureService(
        IStructureNodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StructureNodeDto>> GetTreeAsync()
    {
        var roots = await _repository.GetRootsAsync();

        var result = new List<StructureNodeDto>();

        foreach (var root in roots)
        {
            var dto = await BuildNodeRecursiveAsync(root);

            result.Add(dto);
        }

        return result;
    }

    public async Task<StructureNodeDto?> GetByIdAsync(Guid id)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            return null;

        return await BuildNodeRecursiveAsync(node);
    }

    public async Task<Guid> CreateNodeAsync(CreateStructureNodeRequest request)
    {
        var node = new StructureNode
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            ParentId = request.ParentId,
            Order = request.Order,
            IsActive = true
        };

        await _repository.AddAsync(node);

        await _repository.SaveChangesAsync();

        return node.Id;
    }

    public async Task UpdateNodeAsync(
        Guid id,
        UpdateStructureNodeRequest request)
    {
        var node = await _repository.GetByIdAsync(id);

        if (node == null)
            throw new Exception("Node not found");

        node.Name = request.Name;
        node.Type = request.Type;
        node.Order = request.Order;
        node.IsActive = request.IsActive;

        await _repository.UpdateAsync(node);

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteNodeAsync(Guid id)
    {
        await _repository.SoftDeleteAsync(id);

        await _repository.SaveChangesAsync();
    }

    private async Task<StructureNodeDto> BuildNodeRecursiveAsync(
        StructureNode node)
    {
        var dto = new StructureNodeDto
        {
            Id = node.Id,
            Name = node.Name,
            Type = node.Type,
            ParentId = node.ParentId,
            Order = node.Order,
            IsActive = node.IsActive
        };

        var children = await _repository.GetChildrenAsync(node.Id);

        foreach (var child in children)
        {
            var childDto = await BuildNodeRecursiveAsync(child);

            dto.Children.Add(childDto);
        }

        return dto;
    }
}