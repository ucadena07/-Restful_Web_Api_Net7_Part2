﻿using MagicVilla_VillaAPI.Models;
using MagicVilla_VillaAPI.Models.Dto;

namespace MagicVilla_VillaAPI.Repository.IRepostiory
{
    public interface IUserRepository
    {
        bool IsUniqueUser(string username);
        Task<TokenDTO> Login(LoginRequestDTO loginRequestDTO);
        Task<TokenDTO> RefreshAccessToken(TokenDTO token);
        Task<UserDTO> Register(RegisterationRequestDTO registerationRequestDTO);
        Task RevokeRefreshTokens(TokenDTO token);
    }
}
