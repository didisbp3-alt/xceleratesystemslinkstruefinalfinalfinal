using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    public class ConnectionsController : ApiControllerBase
    {
        private readonly ILogger<ConnectionsController> _logger;

        public ConnectionsController(IHttpClientFactory httpFactory, ILogger<ConnectionsController> logger, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
        }

        // GET: /Connections – show my accepted connections + pending requests
        public async Task<IActionResult> Index()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();

            // Get accepted connections with user details
            var myResp = await client.GetAsync("api/connections/my-with-users");
            var connections = myResp.IsSuccessStatusCode
                ? await myResp.Content.ReadFromJsonAsync<IEnumerable<ConnectionWithUserDTO>>() ?? Array.Empty<ConnectionWithUserDTO>()
                : Array.Empty<ConnectionWithUserDTO>();

            // Get pending incoming requests
            var pendingResp = await client.GetAsync("api/connections/pending");
            var pending = pendingResp.IsSuccessStatusCode
                ? await pendingResp.Content.ReadFromJsonAsync<IEnumerable<ConnectionWithUserDTO>>() ?? Array.Empty<ConnectionWithUserDTO>()
                : Array.Empty<ConnectionWithUserDTO>();

            ViewBag.Pending = pending;
            return View(connections);
        }

        // POST: /Connections/Connect – send connection request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Connect(int targetUserId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.PostAsJsonAsync("api/connections/request", new { AddresseeId = targetUserId });

            TempData["ConnectionMsg"] = resp.IsSuccessStatusCode
                ? "Pedido de ligação enviado!"
                : (resp.StatusCode == System.Net.HttpStatusCode.Conflict ? "Já tens uma ligação pendente ou aceite com este utilizador." : "Não foi possível enviar o pedido.");

            return RedirectBack(targetUserId);
        }

        // POST: /Connections/Accept – accept a pending connection request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int connectionId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.PostAsync($"api/connections/{connectionId}/accept", null);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Connections/Remove – remove/decline a connection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int connectionId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.DeleteAsync($"api/connections/{connectionId}");

            return RedirectToAction(nameof(Index));
        }

        private IActionResult RedirectBack(int userId)
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
                return Redirect(referer);
            return RedirectToAction("Details", "Users", new { id = userId });
        }

        // DTOs
        public class ConnectionWithUserDTO
        {
            public int ConnectionId { get; set; }
            public int RequesterUserId { get; set; }
            public int AddresseeUserId { get; set; }
            public byte Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? AcceptedAt { get; set; }
            // For accepted connections: the other user
            public OtherUserInfo? OtherUser { get; set; }
            // For pending: requester info
            public OtherUserInfo? RequesterUser { get; set; }
            // For accepted connections: addressee info
            public OtherUserInfo? AddresseeUser { get; set; }
        }

        public class OtherUserInfo
        {
            public int UserId { get; set; }
            public string? Name { get; set; }
            public string? ProfileBio { get; set; }
            public string? ProfilePictureUrl { get; set; }
            public int? Role { get; set; }
        }
    }
}
