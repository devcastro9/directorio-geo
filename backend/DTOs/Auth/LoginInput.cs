using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Auth;

/// <summary>Entrada para la autenticación de usuarios mediante credenciales.</summary>
public sealed record LoginInput(
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico es inválido.")]
    [StringLength(254, MinimumLength = 3, ErrorMessage = "El correo debe tener entre 3 y 254 caracteres.")]
    string Email,

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres.")]
    string Password
);
