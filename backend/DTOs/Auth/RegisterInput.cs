using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Auth;

/// <summary>Entrada para el registro de nuevos usuarios en el sistema (fase P2).</summary>
public sealed record RegisterInput(
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico es inválido.")]
    [StringLength(254, MinimumLength = 3, ErrorMessage = "El correo debe tener entre 3 y 254 caracteres.")]
    string Email,

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres.")]
    string Password,

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "El nombre debe tener entre 1 y 150 caracteres.")]
    string FirstName,

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "El apellido debe tener entre 1 y 150 caracteres.")]
    string LastName
);
