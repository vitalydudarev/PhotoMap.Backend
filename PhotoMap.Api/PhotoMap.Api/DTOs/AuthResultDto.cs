using System;

namespace PhotoMap.Api.DTOs;

public class AuthResultDto
{
    public required string Token { get; set; }
    public required DateTimeOffset TokenExpiresOn { get; set; }
}