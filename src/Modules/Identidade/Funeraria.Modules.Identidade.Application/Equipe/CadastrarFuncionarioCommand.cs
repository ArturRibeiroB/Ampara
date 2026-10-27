using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application.Equipe;

/// <summary>
/// O gestor cadastra um funcionário ou consultor (AUT-07, DEC-11). O gestor não define senha de outra pessoa:
/// quem ainda não tem conta recebe o convite para definir a senha; quem já tem conta na plataforma recebe só
/// o aviso de acesso (DEC-02). O e-mail sai pelo evento de vínculo criado.
/// </summary>
public sealed record CadastrarFuncionarioCommand(
    string Nome,
    string Cpf,
    string Email,
    string? Cargo,
    string? Telefone,
    PerfilVinculo? Perfil)
    : ICommand<MembroEquipeResponse>;

internal sealed class CadastrarFuncionarioCommandValidator : AbstractValidator<CadastrarFuncionarioCommand>
{
    public CadastrarFuncionarioCommandValidator()
    {
        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(150).WithMessage("Nome deve ter no máximo 150 caracteres.");
        RuleFor(c => c.Cpf).Cpf();
        RuleFor(c => c.Email).EmailObrigatorio();
        RuleFor(c => c.Telefone).Telefone();
        RuleFor(c => c.Cargo).MaximumLength(100).WithMessage("Cargo deve ter no máximo 100 caracteres.");
        RuleFor(c => c.Perfil)
            .Must(p => p is null or PerfilVinculo.Funcionario or PerfilVinculo.Consultor)
            .WithMessage("Perfil inválido. Use FUNCIONARIO ou CONSULTOR.");
    }
}

public sealed class CadastrarFuncionarioCommandHandler(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    IFuncionarioRepository funcionarios,
    IIdentidadeUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IAuditoria auditoria)
    : ICommandHandler<CadastrarFuncionarioCommand, MembroEquipeResponse>
{
    public async ValueTask<Result<MembroEquipeResponse>> Handle(CadastrarFuncionarioCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        string cpf = DocumentosBrasileiros.Normalizar(command.Cpf);
        PerfilVinculo perfil = command.Perfil ?? PerfilVinculo.Funcionario;

        if (await funcionarios.ExisteCpfAsync(idTenant, cpf, cancellationToken))
        {
            return EquipeErros.CpfDuplicado;
        }

        UsuarioInfo? usuario = await usuarios.ObterPorEmailAsync(command.Email, cancellationToken);
        if (usuario is not null)
        {
            UsuarioVinculo? existente = await vinculos.ObterAsync(usuario.Id, idTenant, cancellationToken);
            if (existente is not null)
            {
                return existente.Perfil == PerfilVinculo.Cliente ? EquipeErros.EmailDeCliente : EquipeErros.JaEhDaEquipe;
            }
        }
        else
        {
            usuario = await usuarios.CriarConvidadoAsync(command.Email, command.Nome, cancellationToken);
        }

        var funcionario = Funcionario.Criar(idTenant, usuario.Id, command.Nome, cpf, command.Cargo, command.Telefone);
        funcionarios.Adicionar(funcionario);
        vinculos.Adicionar(UsuarioVinculo.Criar(usuario.Id, idTenant, perfil));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException e)
        {
            // Dois cadastros ao mesmo tempo: os índices únicos garantem a regra.
            return e.Restricao?.Contains("funcionarios", StringComparison.Ordinal) == true
                ? EquipeErros.CpfDuplicado
                : EquipeErros.JaEhDaEquipe;
        }

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.FuncionarioCadastrado, "Usuario", usuario.Id,
            ValoresNovos: new { perfil = perfil.ToString(), funcionario.Nome, funcionario.Cargo }), cancellationToken);

        return new MembroEquipeResponse(
            usuario.Id, funcionario.Id, funcionario.Nome, usuario.Email, perfil, Ativo: true, funcionario.Cpf, funcionario.Cargo, funcionario.Telefone);
    }
}

/// <summary>Pessoa da equipe da funerária (AUT-07, AUT-08). Gestores criados no onboarding não têm dados de funcionário.</summary>
/// <param name="IdUsuario">Conta da pessoa; é o id usado em /gestao/usuarios/{id}.</param>
/// <param name="IdFuncionario">Cadastro de funcionário; vazio para gestores criados no onboarding.</param>
/// <param name="Nome">Nome da pessoa.</param>
/// <param name="Email">E-mail da conta.</param>
/// <param name="Perfil">FUNERARIA, FUNCIONARIO ou CONSULTOR.</param>
/// <param name="Ativo">Vínculo ativo com a funerária.</param>
/// <param name="Cpf">CPF, quando houver cadastro de funcionário.</param>
/// <param name="Cargo">Cargo, quando informado.</param>
/// <param name="Telefone">Telefone, quando informado.</param>
public sealed record MembroEquipeResponse(
    Guid IdUsuario,
    Guid? IdFuncionario,
    string Nome,
    string Email,
    PerfilVinculo Perfil,
    bool Ativo,
    string? Cpf,
    string? Cargo,
    string? Telefone);
