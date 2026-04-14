let barcodeGerado = null;

// 🔹 GERAR BOLETO
async function gerarCodigo() {
  const data = document.getElementById("data").value;
  const valor = document.getElementById("valor").value;

  if (!data || !valor) {
    alert("Preencha a data e o valor");
    return;
  }

  try {
    console.log("Iniciando geração...");

    const response = await fetch("http://localhost:7232/api/barcode-generate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        dataVencimento: data,
        valor: Number(valor),
      }),
    });

    const dataResponse = await response.json();
    console.log("Generate:", dataResponse);

    // salva o barcode para usar depois
    barcodeGerado = dataResponse.barcode;

    // mostra resultado
    document.getElementById("resultado").style.display = "block";
    document.getElementById("barcode").innerText = dataResponse.barcode;

    document.getElementById("imagem").src =
      "data:image/png;base64," + dataResponse.imagemBase64;
  } catch (erro) {
    console.error("Erro ao gerar:", erro);
    alert("Erro ao gerar boleto");
  }
}

// 🔹 VALIDAR BOLETO
async function validarCodigo() {
  if (!barcodeGerado) {
    alert("Gere um código primeiro");
    return;
  }

  try {
    console.log("Iniciando validação...");

    const response = await fetch("http://localhost:7004/api/barcode-validate", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        barcode: barcodeGerado,
      }),
    });

    const result = await response.json();
    console.log("Validate:", result);

    document.getElementById("mensagem").innerText = result.mensagem;

    const barcodeElement = document.getElementById("barcode");

    barcodeElement.classList.remove("barcode-valid", "barcode-invalid");

    if (result.valido) {
      barcodeElement.classList.add("barcode-valid");
    } else {
      barcodeElement.classList.add("barcode-invalid");
    }
  } catch (erro) {
    console.error("Erro ao validar:", erro);
    alert("Erro ao validar boleto");
  }
}

// FUNÇÃO PRINCIPAL (BOTÃO)
async function gerarEValidar() {
  await gerarCodigo();
  await validarCodigo();
}
