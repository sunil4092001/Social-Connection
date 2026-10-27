using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.Response
{
    public static class ApiResponseFactory
    {

        public static ApiResponse<T> Success<T>( T data, string message = "Request completed successfully.", string? correlationId = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                CorrelationId = correlationId
            };
        }

        public static ApiResponse<T> Failure<T>( string message, Dictionary<string, string[]>? errors = null,string? correlationId = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                // Flatten { "Field": ["msg1", "msg2"] } into ["Field: msg1", "Field: msg2"].
                Errors = errors?.SelectMany(entry => entry.Value.Select(error => $"{entry.Key}: {error}")).ToList(),
                CorrelationId = correlationId
            };
        }
    }
}
