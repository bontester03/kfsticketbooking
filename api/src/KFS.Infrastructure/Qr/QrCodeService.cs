using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KFS.Application.Common.Exceptions;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using KFS.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QRCoder;

namespace KFS.Infrastructure.Qr;

public class QrCodeService : IQrCodeService
{
    private readonly QrSettings _settings;

    public QrCodeService(IOptions<QrSettings> options) => _settings = options.Value;

    public string EncodePayload(QrPayloadInput input)
    {
        var claims = new List<Claim>
        {
            new("tid", input.TicketId.ToString()),
            new("eid", input.EventId.ToString()),
            new("typ", input.ItemType == ScannedItemType.BookingItem ? "bk" : "ap"),
            new("zn",  ((int)input.Zone).ToString()),
            new("sc",  input.SeatsCount.ToString())
        };
        if (!string.IsNullOrEmpty(input.SeatLabel)) claims.Add(new Claim("sl", input.SeatLabel));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Issuer, claims,
            expires: input.ExpiresAt, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public QrPayloadDecoded DecodePayload(string token)
    {
        // Read claims off the validated token directly. The ClaimsPrincipal that ValidateToken
        // produces remaps inbound claim types, so the short names ("tid", "eid", …) we wrote
        // wouldn't be found via principal.FindFirstValue — jwt.Claims preserves them verbatim.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer, ValidAudience = _settings.Issuer,
            IssuerSigningKey = key, ClockSkew = TimeSpan.FromMinutes(1)
        }, out var validated);
        var jwt = (JwtSecurityToken)validated;

        string? Claim(string n) => jwt.Claims.FirstOrDefault(c => c.Type == n)?.Value;
        Guid Get(string n) => Guid.Parse(Claim(n)
            ?? throw new AppException("invalid_qr", $"Missing claim {n}", 400));

        var typ = Claim("typ");
        var zn = int.Parse(Claim("zn") ?? "0");
        var sc = int.Parse(Claim("sc") ?? "1");
        var sl = Claim("sl");

        return new QrPayloadDecoded(Get("tid"), Get("eid"),
            typ == "bk" ? ScannedItemType.BookingItem : ScannedItemType.AdminPass,
            (ZoneCode)zn, sl, sc, jwt.ValidTo);
    }

    public byte[] RenderPng(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }
}
