using Microsoft.AspNetCore.Mvc;
using AttendanceManagementSystem.Services;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Login user with email and password
        /// </summary>
        /// <param name="loginRequest">Login credentials</param>
        /// <returns>Login result with user information</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            try
            {
                if (string.IsNullOrEmpty(loginRequest.Email) || string.IsNullOrEmpty(loginRequest.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password are required" });
                }

                var user = await _authService.LoginAsync(loginRequest.Email, loginRequest.Password);
                
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid email or password" });
                }

                return Ok(new { 
                    success = true, 
                    message = "Login successful",
                    user = new {
                        id = user.Id,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email,
                        role = user.Role?.Name,
                        section = user.Section?.Name
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for email: {Email}", loginRequest.Email);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        /// <param name="registerRequest">User registration data</param>
        /// <returns>Registration result</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
        {
            try
            {
                if (string.IsNullOrEmpty(registerRequest.Email) || string.IsNullOrEmpty(registerRequest.Password))
                {
                    return BadRequest(new { success = false, message = "Email and password are required" });
                }

                var user = new User
                {
                    FirstName = registerRequest.FirstName,
                    LastName = registerRequest.LastName,
                    Email = registerRequest.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerRequest.Password),
                    Phone = registerRequest.Phone,
                    Address = registerRequest.Address,
                    RoleId = registerRequest.RoleId,
                    SectionId = registerRequest.SectionId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var success = await _authService.RegisterAsync(user);
                
                if (!success)
                {
                    return BadRequest(new { success = false, message = "Registration failed. Email might already exist." });
                }

                return Ok(new { 
                    success = true, 
                    message = "User registered successfully",
                    user = new {
                        id = user.Id,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error for email: {Email}", registerRequest.Email);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Test endpoint to verify API is working
        /// </summary>
        /// <returns>API status</returns>
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { 
                success = true, 
                message = "API is working",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public int RoleId { get; set; }
        public int? SectionId { get; set; }
    }
}
