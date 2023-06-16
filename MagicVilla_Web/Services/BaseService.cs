using AutoMapper.Internal;
using MagicVilla_Utility;
using MagicVilla_Web.Models;
using MagicVilla_Web.Models.Dto;
using MagicVilla_Web.Services.IServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace MagicVilla_Web.Services
{
    public class BaseService : IBaseService
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _villaApiUrl;
        public APIResponse responseModel { get; set; }
        public IHttpClientFactory httpClient { get; set; }
        
        public BaseService(IHttpClientFactory httpClient, ITokenProvider tokenProvider, 
            IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            this.responseModel = new();
            this.httpClient = httpClient;
            _tokenProvider = tokenProvider;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _villaApiUrl = _configuration.GetValue<string>("ServiceUrls:VillaAPI");
        }
        public async Task<T> SendAsync<T>(APIRequest apiRequest)
        {
            try
            {
                var client = httpClient.CreateClient("MagicAPI");


                var messageFactory = () =>
                {
                    HttpRequestMessage message = new HttpRequestMessage();





                    if (apiRequest.ContentType == SD.ContentType.MultipartFormData)
                    {
                        message.Headers.Add("Accept", "*/*");
                    }
                    else
                    {
                        message.Headers.Add("Accept", "application/json");
                    }

                    message.RequestUri = new Uri(apiRequest.Url);
                    if (apiRequest.WithBearer && _tokenProvider.GetToken() != null)
                    {
                        var token = _tokenProvider.GetToken();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                    }

                    if (apiRequest.ContentType == SD.ContentType.MultipartFormData)
                    {
                        var content = new MultipartFormDataContent();
                        foreach (var prop in apiRequest.Data.GetType().GetProperties())
                        {
                            var value = prop.GetValue(apiRequest.Data);
                            if (value is FormFile)
                            {
                                var file = (FormFile)value;
                                if (file is not null)
                                {
                                    content.Add(new StreamContent(file.OpenReadStream()), prop.Name, file.FileName);
                                }
                            }
                            else
                            {
                                content.Add(new StringContent(value == null ? "" : value.ToString()), prop.Name);
                            }
                        }
                        message.Content = content;
                    }
                    else
                    {
                        if (apiRequest.Data != null)
                        {
                            message.Content = new StringContent(JsonConvert.SerializeObject(apiRequest.Data),
                                Encoding.UTF8, "application/json");
                        }
                    }


                    switch (apiRequest.ApiType)
                    {
                        case SD.ApiType.POST:
                            message.Method = HttpMethod.Post;
                            break;
                        case SD.ApiType.PUT:
                            message.Method = HttpMethod.Put;
                            break;
                        case SD.ApiType.DELETE:
                            message.Method = HttpMethod.Delete;
                            break;
                        default:
                            message.Method = HttpMethod.Get;
                            break;

                    }
                    return message; 
                };

            

                HttpResponseMessage httpResponseMessage = null;

       
                httpResponseMessage = await SendWithRefreshTokenAsync(client, messageFactory,apiRequest.WithBearer);

                APIResponse FinalApiReponse = new()
                {
                    IsSuccess = false
                };

                try
                {

                    switch (httpResponseMessage.StatusCode)
                    {
                        case System.Net.HttpStatusCode.NotFound:
                            FinalApiReponse.ErrorMessages = new List<string>() { "Not Found"};
                            break;
                        case System.Net.HttpStatusCode.Forbidden:
                            FinalApiReponse.ErrorMessages = new List<string>() { "Forbidden" };
                            break;
                        case System.Net.HttpStatusCode.Unauthorized:
                            FinalApiReponse.ErrorMessages = new List<string>() { "Unauthorized" };
                            break;
                        case System.Net.HttpStatusCode.InternalServerError:
                            FinalApiReponse.ErrorMessages = new List<string>() { "InternalServerError" };
                            break;
                        default:

                            var apiContent = await httpResponseMessage.Content.ReadAsStringAsync();
                            FinalApiReponse.IsSuccess = true;
                            FinalApiReponse = JsonConvert.DeserializeObject<APIResponse>(apiContent);
                            break;
                    }

    
                    
                }
                catch(AuthException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    FinalApiReponse.ErrorMessages = new List<string>() { "Error encounter" };
                }
                var res = JsonConvert.SerializeObject(FinalApiReponse);
                var returnObj = JsonConvert.DeserializeObject<T>(res);
                return returnObj;

            }
            catch (AuthException)
            {
                throw;
            }
            catch (Exception e)
            {
                var dto = new APIResponse
                {
                    ErrorMessages = new List<string> { Convert.ToString(e.Message) },
                    IsSuccess = false
                };
                var res = JsonConvert.SerializeObject(dto);
                var APIResponse = JsonConvert.DeserializeObject<T>(res);
                return APIResponse;
            }
        }
        async Task<HttpResponseMessage> SendWithRefreshTokenAsync(HttpClient httpClient, Func<HttpRequestMessage> httpRequestMessageFactory, bool withBearer = true)
        {
            if(!withBearer)
            {
                return await httpClient.SendAsync(httpRequestMessageFactory());
            }
            else
            {

                TokenDTO tokenDTO = _tokenProvider.GetToken();
                if(tokenDTO != null && !string.IsNullOrEmpty(tokenDTO.AccessToken))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDTO.AccessToken);

                }
                HttpResponseMessage resp = new();
                try
                {
                    resp = await httpClient.SendAsync(httpRequestMessageFactory());
                    if (resp.IsSuccessStatusCode)
                    {
                        return resp;
                    }
                    if(!resp.IsSuccessStatusCode && resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        await InvokeRefreshTokenEndpoint(httpClient, tokenDTO.AccessToken,tokenDTO.RefreshToken);
                        resp = await httpClient.SendAsync(httpRequestMessageFactory());
                        return resp;
                    }
                    return resp;
                }
                catch(AuthException authEx)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        await InvokeRefreshTokenEndpoint(httpClient, tokenDTO.AccessToken, tokenDTO.RefreshToken);
                        resp = await httpClient.SendAsync(httpRequestMessageFactory());
                        return resp;
                    }
                    throw;
                }

            }
        }

        async Task InvokeRefreshTokenEndpoint(HttpClient httpClient, string existingAccessToken, string existingRefreshToken)
        {
            HttpRequestMessage message = new HttpRequestMessage();
            message.Headers.Add("Accept", "application/json");
            message.RequestUri = new Uri($"{_villaApiUrl}/api/{SD.CurrentAPIVersion}/UsersAuth/refresh");
            message.Method = HttpMethod.Post;
            message.Content = new StringContent(JsonConvert.SerializeObject(new TokenDTO()
            {
                AccessToken = existingAccessToken,
                RefreshToken = existingRefreshToken 
            }), Encoding.UTF8, "application/json");
            
            var resp = await httpClient.SendAsync(message); 
            var content = await resp.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<APIResponse>(content);

            if (apiResponse?.IsSuccess != true)
            {
                await _httpContextAccessor.HttpContext.SignOutAsync();
                _tokenProvider.ClearToken();
                throw new AuthException();
            }
            else
            {
                var tokenDataStr = JsonConvert.SerializeObject(apiResponse.Result);
                var tokenDto = JsonConvert.DeserializeObject<TokenDTO>(tokenDataStr);

                if(tokenDto != null && !string.IsNullOrEmpty(tokenDto.AccessToken)) 
                {
                    await SignInWithNewTokens(tokenDto);
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenDto.AccessToken);

                }
            }
        }

        async Task SignInWithNewTokens(TokenDTO tokenDTO)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokenDTO.AccessToken);

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, jwt.Claims.FirstOrDefault(u => u.Type == "unique_name").Value));
            identity.AddClaim(new Claim(ClaimTypes.Role, jwt.Claims.FirstOrDefault(u => u.Type == "role").Value));
            var principal = new ClaimsPrincipal(identity);
            await _httpContextAccessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            _tokenProvider.SetToken(tokenDTO);
        }
    }
}
