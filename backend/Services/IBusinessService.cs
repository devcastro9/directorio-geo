using backend.DTOs;
using backend.DTOs.Management;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

public interface IBusinessService
{
    Task<PagedResult<ManagedSummaryDto>> ListAsync(ManagementListRequest request, CancellationToken ct = default);
    Task<ManagedBusinessDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ManagedBusinessDto> CreateAsync(CreateBusinessRequest request, CancellationToken ct = default);
    Task<ManagedBusinessDto> UpdateAsync(Guid id, BusinessInputDto input, CancellationToken ct = default);
    Task<ManagedBusinessDto> SetPublicationAsync(Guid id, PublicationInputDto input, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<ManagedBusinessDto> RestoreAsync(Guid id, RestoreInputDto input, CancellationToken ct = default);

    Task<(ManagedBusinessDto Business, Guid BranchId)> AddBranchAsync(Guid businessId, BranchInputDto input, CancellationToken ct = default);
    Task<ManagedBusinessDto> UpdateBranchAsync(Guid branchId, BranchInputDto input, CancellationToken ct = default);
    Task SoftDeleteBranchAsync(Guid branchId, CancellationToken ct = default);
    Task<ManagedBusinessDto> RestoreBranchAsync(Guid branchId, CancellationToken ct = default);

    Task<(ManagedBusinessDto Business, Guid PhotoId)> AddPhotoAsync(Guid branchId, IFormFile file, PhotoInputDto meta, CancellationToken ct = default);
    Task<ManagedBusinessDto> UpdatePhotoMetaAsync(Guid photoId, PhotoInputDto input, CancellationToken ct = default);
    Task SoftDeletePhotoAsync(Guid photoId, CancellationToken ct = default);

    Task<(byte[] Data, string ContentType)> GetPhotoAsync(Guid photoId, bool isAdmin, CancellationToken ct = default);
}
