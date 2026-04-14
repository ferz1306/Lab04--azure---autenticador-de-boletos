using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace fnValidaBoleto;

public class ValidaBoletos
{
    private readonly ILogger<ValidaBoletos> _logger;

    public ValidaBoletos(ILogger<ValidaBoletos> logger)
    {
        _logger = logger;
    }

    [Function("barcode-validate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string barcode = data?.barcode;

            if (string.IsNullOrEmpty(barcode))
            {
                return new BadRequestObjectResult("Código de barras é obrigatório");
            }

            if (barcode.Length != 44)
            {
                var result = new
                {
                    valido = false,
                    mensagem = "Código de barras deve conter 44 caracteres"
                };
                return new BadRequestObjectResult(result);
            }

            string datePart = barcode.Substring(3, 8);

            if (DateTime.TryParseExact(
                datePart,
                "yyyyMMdd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime dateObj))
            {
                if (dateObj.Date < DateTime.Now.Date)
                {
                    var result = new
                    {
                        valido = false,
                        mensagem = "Boleto vencido"
                    };
                    return new BadRequestObjectResult(result);
                }
            }
            else
            {
                var result = new
                {
                    valido = false,
                    mensagem = "Data de vencimento inválida"
                };
                return new BadRequestObjectResult(result);
            }

            var resultOk = new
            {
                valido = true,
                mensagem = "Boleto válido",
                vencimento = dateObj.ToString("yyyy-MM-dd")
            };

            return new OkObjectResult(resultOk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar boleto");
            return new BadRequestObjectResult("Erro ao processar validação");
        }
    }
}