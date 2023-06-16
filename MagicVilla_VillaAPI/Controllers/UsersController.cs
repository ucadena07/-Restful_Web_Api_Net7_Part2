using MagicVilla_VillaAPI.Models;
using MagicVilla_VillaAPI.Models.Dto;
using MagicVilla_VillaAPI.Repository.IRepostiory;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace MagicVilla_VillaAPI.Controllers
{
    [Route("api/v{version:apiVersion}/UsersAuth")]
    [ApiController]
    [ApiVersionNeutral]
    public class UsersController : Controller
    {
        private readonly IUserRepository _userRepo;
        protected APIResponse _response;
        public UsersController(IUserRepository userRepo)
        {
            _userRepo = userRepo;
            _response = new();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO model)
        {
            var loginResponse = await _userRepo.Login(model);
            if (loginResponse == null || string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Username or password is incorrect");
                return BadRequest(_response);
            }
            _response.StatusCode = HttpStatusCode.OK;
            _response.IsSuccess = true;
            _response.Result = loginResponse;
            return Ok(_response);
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] TokenDTO model)
        {
            if (ModelState.IsValid)
            {
                await _userRepo.RevokeRefreshTokens(model);
                _response.StatusCode = HttpStatusCode.OK;
                _response.IsSuccess = true;
                return Ok(_response);
            }
            return BadRequest(_response);

        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterationRequestDTO model)
        {
            bool ifUserNameUnique = _userRepo.IsUniqueUser(model.UserName);
            if (!ifUserNameUnique)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Username already exists");
      
            }

            var user = await _userRepo.Register(model);
            if (user == null)
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Error while registering");
                return BadRequest(_response);
            }
            _response.StatusCode = HttpStatusCode.OK;
            _response.IsSuccess = true;
            return Ok(_response);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> GetNewTokenFromRefreshToken([FromBody] TokenDTO tokenDTO)
        {
            if(ModelState.IsValid)
            {
                var tokenDTOReponse = await _userRepo.RefreshAccessToken(tokenDTO);

                if(tokenDTOReponse == null || string.IsNullOrEmpty(tokenDTOReponse.RefreshToken)) 
                {
                    _response.StatusCode = HttpStatusCode.BadRequest;
                    _response.IsSuccess = false;
                    _response.ErrorMessages.Add("Token Invalid");
                    return BadRequest(_response);
            
                }
                else
                {
                    _response.StatusCode = HttpStatusCode.OK;
                    _response.IsSuccess = true;
                    _response.Result = tokenDTOReponse;    
                    return Ok(_response);
                }

   
            }
            else
            {
                _response.StatusCode = HttpStatusCode.BadRequest;
                _response.IsSuccess = false;
                _response.ErrorMessages.Add("Invalid Input");
                return BadRequest(_response);
            }


        }
    }
}
