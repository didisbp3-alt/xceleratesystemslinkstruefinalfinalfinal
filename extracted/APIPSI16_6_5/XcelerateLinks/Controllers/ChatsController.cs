using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using APIPSI16.Models;
using APIPSI16.Services;
using APIPSI16.Models.DTOs;

namespace XcelerateLinks.Mvc.Controllers
{
    public class ChatsController : ApiControllerBase
    {
        private readonly ILogger<ChatsController> _logger;

        public ChatsController(IHttpClientFactory httpFactory, ILogger<ChatsController> logger, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
        }

        // Role-dispatched: admin → Index (table), user → Messages (React chat)
        public async Task<IActionResult> Index()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (!IsAdmin())
                return RedirectToAction(nameof(Messages));

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync("api/chat");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = await SafeReadStringAsync(resp) ?? "Unable to load chats.";
                return View(Array.Empty<Chat>());
            }

            var chats = await resp.Content.ReadFromJsonAsync<IEnumerable<Chat>>();
            return View(chats ?? Array.Empty<Chat>());
        }

        // USER-FACING: full messaging page (React + SignalR)
        public IActionResult Messages(int? chatId = null)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            ViewBag.InitialChatId = chatId;
            ViewBag.CurrentUserId = GetCurrentUserId();
            return View();
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/chat/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var chatDto = await resp.Content.ReadFromJsonAsync<ChatDetailDTO>();
            if (chatDto == null) return RedirectToAction(nameof(Index));
            return View(chatDto);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? withUserId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            // Load user list so we can show a people-picker
            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync("api/users/network");
            var users = resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<IEnumerable<APIPSI16.Models.DTOs.UserDTO>>() ?? Array.Empty<APIPSI16.Models.DTOs.UserDTO>()
                : Array.Empty<APIPSI16.Models.DTOs.UserDTO>();

            var myId = GetCurrentUserId();
            users = users.Where(u => u.UserId != myId).ToArray();
            ViewBag.Users = users;
            ViewBag.PreselectedUserId = withUserId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(List<int> participantIds, string? chatName = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            if (participantIds == null || participantIds.Count == 0)
            {
                ViewBag.Error = "Seleciona pelo menos um participante.";
                // Re-load users
                var client2 = CreateAuthorizedClient();
                var resp2 = await client2.GetAsync("api/users/network");
                var users2 = resp2.IsSuccessStatusCode
                    ? await resp2.Content.ReadFromJsonAsync<IEnumerable<APIPSI16.Models.DTOs.UserDTO>>() ?? Array.Empty<APIPSI16.Models.DTOs.UserDTO>()
                    : Array.Empty<APIPSI16.Models.DTOs.UserDTO>();
                ViewBag.Users = users2.Where(u => u.UserId != userId).ToArray();
                ViewBag.PreselectedUserId = (int?)null;
                return View();
            }

            // Include the creator in participant list
            if (!participantIds.Contains(userId.Value))
                participantIds.Insert(0, userId.Value);

            var client = CreateAuthorizedClient();

            // Create chat with all participants atomically
            var payload = new
            {
                ParticipantIds = participantIds,
                ChatName = chatName
            };

            var createResp = await client.PostAsJsonAsync("api/chat/with-participants", payload);
            if (!createResp.IsSuccessStatusCode)
            {
                ViewBag.Error = await SafeReadStringAsync(createResp) ?? "Não foi possível criar a conversa.";
                var client2 = CreateAuthorizedClient();
                var resp2 = await client2.GetAsync("api/users/network");
                ViewBag.Users = resp2.IsSuccessStatusCode
                    ? (await resp2.Content.ReadFromJsonAsync<IEnumerable<APIPSI16.Models.DTOs.UserDTO>>() ?? Array.Empty<APIPSI16.Models.DTOs.UserDTO>())
                        .Where(u => u.UserId != userId).ToArray()
                    : Array.Empty<APIPSI16.Models.DTOs.UserDTO>();
                ViewBag.PreselectedUserId = (int?)null;
                return View();
            }

            var created = await createResp.Content.ReadFromJsonAsync<APIPSI16.Models.Chat>();
            if (created == null) return RedirectToAction(nameof(Messages));

            return RedirectToAction(nameof(Messages), new { chatId = created.ChatId });        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/chat/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var chat = await resp.Content.ReadFromJsonAsync<Chat>();
            if (chat == null) return RedirectToAction(nameof(Index));
            return View(chat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.DeleteAsync($"api/chat/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Delete), new { id });

            return RedirectToAction(nameof(Index));
        }
    }
}
