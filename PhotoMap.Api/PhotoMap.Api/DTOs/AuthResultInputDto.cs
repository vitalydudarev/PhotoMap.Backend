using System;

namespace PhotoMap.Api.DTOs;

public class AuthResultInputDto
{
    public required string Token { get; set; }
    public required long TokenExpiresIn { get; set; }
}