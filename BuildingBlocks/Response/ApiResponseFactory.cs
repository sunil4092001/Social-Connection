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
                Errors = (IEnumerable<string>)errors,
                CorrelationId = correlationId
            };
        }
    }
}
