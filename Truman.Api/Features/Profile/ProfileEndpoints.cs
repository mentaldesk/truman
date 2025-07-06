using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Truman.Data;
using Truman.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Truman.Api.Features.Profile;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/profile", async (HttpContext http, TrumanDbContext db) =>
        {
            var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Email == email);
            if (profile == null) return Results.NotFound();
            var dto = new UserProfileDto
            {
                Mood = profile.Mood,
                SelectedValues = JsonSerializer.Deserialize<List<string>>(profile.SelectedValues) ?? new List<string>()
            };
            return Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPost("/api/profile", async (HttpContext http, TrumanDbContext db, [FromBody] UserProfileDto dto) =>
        {
            var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
            var existing = await db.UserProfiles.FirstOrDefaultAsync(p => p.Email == email);
            if (existing == null)
            {
                var profile = new UserProfile
                {
                    Email = email,
                    Mood = dto.Mood,
                    SelectedValues = JsonSerializer.Serialize(dto.SelectedValues)
                };
                db.UserProfiles.Add(profile);
            }
            else
            {
                existing.Mood = dto.Mood;
                existing.SelectedValues = JsonSerializer.Serialize(dto.SelectedValues);
                db.UserProfiles.Update(existing);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }
} 