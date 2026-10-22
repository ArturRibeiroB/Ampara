using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Commands.CadastrarFuneraria;

public sealed class CadastrarFunerariaCommandHandler(IFunerariaRepository funerarias, ITenantsUnitOfWork unitOfWork)
    : ICommandHandler<CadastrarFunerariaCommand, FunerariaCadastradaResponse>
{
    public async ValueTask<Result<FunerariaCadastradaResponse>> Handle(CadastrarFunerariaCommand command, CancellationToken cancellationToken)
    {
        string cnpj = DocumentosBrasileiros.Normalizar(command.Cnpj);
        string slug = command.Slug.Trim().ToLowerInvariant();

        if (await funerarias.ExisteCnpjAsync(cnpj, cancellationToken))
        {
            return FunerariaErros.CnpjDuplicado;
        }

        if (await funerarias.ExisteSlugAsync(slug, cancellationToken))
        {
            return FunerariaErros.SlugEmUso;
        }

        var funeraria = FunerariaEntity.Cadastrar(
            command.NomeFantasia,
            command.RazaoSocial,
            cnpj,
            slug,
            command.EmailContato,
            command.Telefone,
            command.Origem,
            command.GestorNome,
            command.GestorEmail);

        funerarias.Adicionar(funeraria);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FunerariaCadastradaResponse(funeraria.Id, funeraria.Slug, funeraria.Status);
    }
}
