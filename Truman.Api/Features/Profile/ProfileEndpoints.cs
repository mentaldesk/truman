using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Truman.Data;
using Truman.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Claims;

namespace Truman.Api.Features.Profile;

public class UpdateMoodDto { public int Mood { get; set; } }
public class UpdateValuesDto { public List<string> SelectedValues { get; set; } = new(); }

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/profile", async (HttpContext http, IProfileService profileService) =>
        {
            var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
            var profile = await profileService.GetUserProfileAsync(email);
            if (profile == null) return Results.NotFound();
            var dto = new UserProfileDto
            {
                Mood = profile.Mood,
                SelectedValues = JsonSerializer.Deserialize<List<string>>(profile.SelectedValues) ?? new List<string>()
            };
            return Results.Ok(dto);
        }).RequireAuthorization();

        app.MapPatch("/api/profile/mood", async (HttpContext http, TrumanDbContext db, [FromBody] UpdateMoodDto dto) =>
        {
            var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Email == email);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    Email = email,
                    Mood = dto.Mood,
                    SelectedValues = JsonSerializer.Serialize(new List<string>())
                };
                db.UserProfiles.Add(profile);
            }
            else
            {
                profile.Mood = dto.Mood;
                db.UserProfiles.Update(profile);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();

        app.MapPatch("/api/profile/values", async (HttpContext http, TrumanDbContext db, [FromBody] UpdateValuesDto dto) =>
        {
            var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Email == email);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    Email = email,
                    Mood = 5,
                    SelectedValues = JsonSerializer.Serialize(dto.SelectedValues)
                };
                db.UserProfiles.Add(profile);
            }
            else
            {
                profile.SelectedValues = JsonSerializer.Serialize(dto.SelectedValues);
                db.UserProfiles.Update(profile);
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }
} 