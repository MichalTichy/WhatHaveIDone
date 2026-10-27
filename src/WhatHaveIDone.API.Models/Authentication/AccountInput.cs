using System.ComponentModel.DataAnnotations;

namespace WhatHaveIDone.API.Models;

public sealed class AccountInput
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MinLength(10), MaxLength(128)] public string Password { get; set; } = "";
}
