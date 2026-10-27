using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Identidade.Contracts;

namespace Funeraria.Modules.Clientes.Application.Commands.CadastrarCliente;

/// <remarks>
/// Ordem pensada para não deixar dado pela metade:
/// <list type="number">
/// <item>Confere o CPF antes de tocar na conta.</item>
/// <item>Cria (ou confere) a conta na Identidade. Se o passo 3 falhar, sobra só a conta, que é reaproveitada na
/// próxima tentativa pela própria senha.</item>
/// <item>Grava o cliente. O vínculo CLIENTE é criado na hora, para o login já funcionar, e de novo pelo outbox
/// (<c>ClienteCadastrado</c>) caso essa chamada falhe. A operação é idempotente.</item>
/// </list>
/// </remarks>
public sealed class CadastrarClienteCommandHandler(
    IClienteRepository clientes,
    IClientesUnitOfWork unitOfWork,
    IIdentidadeApi identidade,
    ITenantContext tenantContext)
    : ICommandHandler<CadastrarClienteCommand, ClienteResponse>
{
    public async ValueTask<Result<ClienteResponse>> Handle(CadastrarClienteCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        string cpf = DocumentosBrasileiros.Normalizar(command.Cpf);

        if (await clientes.ExisteCpfAsync(cpf, cancellationToken))
        {
            return ClienteErros.CpfDuplicado;
        }

        Result<Guid> conta = await identidade.ObterOuCriarContaAsync(command.Email, command.Nome, command.Senha, cancellationToken);
        if (conta.IsFailure)
        {
            return Result.Failure<ClienteResponse>(ComMensagemDoCard(conta.Error));
        }

        if (await identidade.PossuiVinculoAsync(conta.Value, idTenant, cancellationToken))
        {
            return ClienteErros.EmailDuplicado;
        }

        var cliente = Cliente.Criar(
            idTenant,
            conta.Value,
            command.Nome,
            cpf,
            command.DataNascimento!.Value,
            command.Email,
            command.Telefone,
            command.Endereco,
            command.Cep is null ? null : DocumentosBrasileiros.Normalizar(command.Cep),
            command.Cidade,
            command.Estado);

        clientes.Adicionar(cliente);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await identidade.VincularClienteAsync(conta.Value, idTenant, cliente.Id, cancellationToken);

        // Aceite marcado na tela de cadastro: registra as versões vigentes com data e IP (SEG-01).
        if (command.AceiteTermos)
        {
            await identidade.RegistrarAceiteTermosAsync(conta.Value, cancellationToken);
        }

        return ClienteResponse.De(cliente);
    }

    /// <summary>Senha fora dos critérios: a mensagem do card primeiro, seguida do que falta na senha.</summary>
    private static Error ComMensagemDoCard(Error erro)
    {
        if (erro is not ValidationError validacao || !validacao.Erros.TryGetValue("senha", out string[]? detalhes))
        {
            return erro;
        }

        var erros = new Dictionary<string, string[]>(validacao.Erros)
        {
            ["senha"] = [ClienteErros.MensagemSenha, .. detalhes]
        };

        return new ValidationError(erros);
    }
}
