using Azure.Messaging.ServiceBus;
using BarcodeStandard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SkiaSharp;

namespace fcGeradorBoletos;

public class GeradorCodigoBarras
{
    private readonly ILogger<GeradorCodigoBarras> _logger;
    private readonly string _serviceBusConnectionString;
    private readonly string _queueName = "gerador-codigo-barras";

    public GeradorCodigoBarras(ILogger<GeradorCodigoBarras> logger)
    {
        _logger = logger;
        _serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
    }

    [Function("barcode-generate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string valor = data?.valor;
            string dataVencimento = data?.dataVencimento;

            // Validação
            if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(dataVencimento))
            {
                return new BadRequestObjectResult("Valor e data de vencimento são obrigatórios");
            }

            // Validar data
            if (!DateTime.TryParseExact(
                dataVencimento,
                "yyyy-MM-dd",
                null,
                System.Globalization.DateTimeStyles.None,
                out DateTime dateObj))
            {
                return new BadRequestObjectResult("Data inválida. Use yyyy-MM-dd");
            }

            string dateString = dateObj.ToString("yyyyMMdd");

            // Validar valor
            if (!decimal.TryParse(valor, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal valorDecimal))
            {
                return new BadRequestObjectResult("Valor inválido");
            }

            int valorCentavos = (int)Math.Round(valorDecimal * 100, MidpointRounding.AwayFromZero);
            string valorStr = valorCentavos.ToString("D8");

            string bankCode = "008";
            string baseCode = string.Concat(bankCode, dateString, valorStr);

            string barcodeData = baseCode.Length > 44
                ? baseCode.Substring(0, 44)
                : baseCode.PadRight(44, '0');

            _logger.LogInformation($"Código gerado: {barcodeData}");

            // Gerar imagem
            Barcode barcode = new Barcode();
            var skImage = barcode.Encode(BarcodeStandard.Type.Code128, barcodeData);

            using var encodedData = skImage.Encode(SKEncodedImageFormat.Png, 100);
            string base64String = Convert.ToBase64String(encodedData.ToArray());

            var resultObject = new
            {
                barcode = barcodeData,
                valorOriginal = valorDecimal,
                dataVencimento = dateObj,
                imagemBase64 = base64String
            };

            await SendFileFallback(resultObject, _serviceBusConnectionString, _queueName);

            return new OkObjectResult(resultObject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar código de barras");
            return new BadRequestObjectResult("Erro ao processar requisição");
        }
    }

    private async Task SendFileFallback(
        object resultObject,
        string serviceBusConnectionString,
        string queueName)
    {
        await using var client = new ServiceBusClient(serviceBusConnectionString);

        ServiceBusSender sender = client.CreateSender(queueName);

        string messageBody = JsonConvert.SerializeObject(resultObject);

        ServiceBusMessage message = new ServiceBusMessage(messageBody);

        await sender.SendMessageAsync(message);

        _logger.LogInformation($"Mensagem enviada para fila {queueName}");
    }
}