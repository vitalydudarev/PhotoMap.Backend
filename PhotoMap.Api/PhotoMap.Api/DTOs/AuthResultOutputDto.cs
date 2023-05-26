using System;

namespace PhotoMap.Api.DTOs;

public class AuthResultOutputDto
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
}