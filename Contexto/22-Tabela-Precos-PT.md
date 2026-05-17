# Tabela de Preços PT para Reparações

Atualizado: 2026-05-16

Objetivo: criar uma tabela base pré-populada para a feature "tabela de preços configurável por tenant" do RepairDesk. Estes valores servem para acelerar onboarding de oficinas em Portugal; **não devem ser apresentados como tabela oficial de mercado**.

## Conclusão curta

O mercado PT de reparações em 2026 tem três realidades:

1. **Cadeias nacionais** como iServices publicam preços com IVA, garantia e reparação rápida. São boas como âncora alta/média.
2. **Oficinas independentes** em Lisboa/Porto competem por preço e qualidade da peça. Muitas distinguem compatível, recondicionado e original.
3. **Interior/Viseu** tende a cobrar menos que Lisboa/Porto, mas sofre mais com stock, portes e menor rotação.

Recomendação para o RepairDesk: importar estes preços como `preco_sugerido`, nunca como `preco_fixo`. No onboarding, perguntar:

- "Queres usar preços de referência nacionais?"
- "Aplicar ajuste local: -15%, 0%, +10%?"
- "Trabalhas com peça compatível, premium ou original?"

## Como ler a tabela

- `pvp_medio_eur`: preço final ao cliente, com IVA quando a fonte pública o mostra assim.
- `custo_peca_eur`: custo estimado de peça para oficina, sem mão de obra; marcado `{{aprox.}}` quando inferido por mercado/fornecedor e não por preço público direto.
- `margem_bruta_pct`: `(PVP - custo_peca) / PVP`, antes de IVA, garantia, comissões, renda, ferramentas, perdas e retrabalho. É uma métrica útil, mas otimista.
- `tempo_min`: tempo técnico típico quando há stock e não há dano adicional.
- `fonte`: fonte direta ou fonte agregada. Quando há várias, usei média/âncora conservadora.

## Tabela CSV-friendly

| marca | modelo | servico | peca | custo_peca_eur | pvp_medio_eur | margem_bruta_pct | tempo_min | fonte |
|---|---|---|---|---:|---:|---:|---:|---|
| Apple | iPhone 11 | Ecrã | LCD/OLED compatível/recondicionado | 30 {{aprox.}} | 77 | 61% | 30 | iLoja/iPartiu |
| Apple | iPhone 11 | Bateria | Bateria compatível/premium | 20 {{aprox.}} | 57 | 65% | 30 | iLoja/iPartiu |
| Apple | iPhone 11 | Vidro traseiro | Vidro traseiro sem chassis | 15 {{aprox.}} | 105 | 86% | 90 | iLoja/iPartiu |
| Apple | iPhone 11 | Conector de carga | Flex Lightning + microfone | 18 {{aprox.}} | 77 | 77% | 60 | iLoja/iPartiu |
| Apple | iPhone 11 | Câmara traseira | Módulo câmara traseira | 35 {{aprox.}} | 107 | 67% | 45 | iLoja/iPartiu |
| Apple | iPhone 12 | Ecrã | OLED compatível/original | 55 {{aprox.}} | 139 | 60% | 30 | iServices/iLoja |
| Apple | iPhone 12 | Bateria | Bateria compatível/premium | 24 {{aprox.}} | 60 | 60% | 30 | iServices/iLoja |
| Apple | iPhone 12 | Vidro traseiro | Vidro traseiro / capa traseira | 20 {{aprox.}} | 165 | 88% | 90 | iServices/iLoja/ReparaJá peça |
| Apple | iPhone 12 | Conector de carga | Flex Lightning + microfone | 25 {{aprox.}} | 105 | 76% | 60 | iServices/iLoja |
| Apple | iPhone 12 | Câmara traseira | Módulo câmara traseira | 45 {{aprox.}} | 100 | 55% | 45 | iServices/iLoja |
| Apple | iPhone 13 | Ecrã | OLED compatível/original | 65 {{aprox.}} | 155 | 58% | 30 | iServices/iPartiu |
| Apple | iPhone 13 | Bateria | Bateria compatível/premium | 28 {{aprox.}} | 75 | 63% | 30 | iServices/iPartiu |
| Apple | iPhone 13 | Vidro traseiro | Capa traseira/vidro | 25 {{aprox.}} | 185 | 86% | 90 | iServices/iPartiu |
| Apple | iPhone 13 | Conector de carga | Flex Lightning + microfone | 28 {{aprox.}} | 120 | 77% | 60 | iServices/iPartiu |
| Apple | iPhone 13 | Câmara traseira | Módulo câmara traseira | 55 {{aprox.}} | 120 | 54% | 45 | iServices/iPartiu |
| Apple | iPhone 14 | Ecrã | OLED compatível/original | 80 {{aprox.}} | 170 | 53% | 30 | iServices/iPartiu |
| Apple | iPhone 14 | Bateria | Bateria compatível/premium | 35 {{aprox.}} | 80 | 56% | 30 | iServices/iPartiu |
| Apple | iPhone 14 | Vidro traseiro | Capa traseira/vidro | 35 {{aprox.}} | 210 | 83% | 90 | iServices/iPartiu |
| Apple | iPhone 14 | Conector de carga | Flex Lightning + microfone | 35 {{aprox.}} | 120 | 71% | 60 | iServices/iPartiu |
| Apple | iPhone 14 | Câmara traseira | Módulo câmara traseira | 65 {{aprox.}} | 120 | 46% | 45 | iServices |
| Apple | iPhone 15 | Ecrã | OLED compatível/original | 110 {{aprox.}} | 217 | 49% | 30 | iServices/iPartiu |
| Apple | iPhone 15 | Bateria | Bateria compatível/premium | 45 {{aprox.}} | 90 | 50% | 30 | iServices/iPartiu |
| Apple | iPhone 15 | Vidro traseiro | Capa traseira/vidro | 45 {{aprox.}} | 230 | 80% | 90 | iServices/iPartiu |
| Apple | iPhone 15 | Conector de carga | Flex USB-C | 40 {{aprox.}} | 120 | 67% | 60 | iServices/iPartiu |
| Apple | iPhone 15 | Câmara traseira | Módulo câmara traseira | 75 {{aprox.}} | 140 | 46% | 45 | iServices/iPartiu |
| Samsung | Galaxy A50 | Ecrã | Ecrã OLED/Service Pack | 45 {{aprox.}} | 103 | 56% | 30 | iServices |
| Samsung | Galaxy A50 | Bateria | Bateria original/compatível | 18 {{aprox.}} | 60 | 70% | 25 | iServices |
| Samsung | Galaxy A50 | Conector de carga | Flex USB-C | 12 {{aprox.}} | 50 | 76% | 45 | iServices |
| Samsung | Galaxy A52 | Ecrã | Ecrã original | 55 {{aprox.}} | 100 | 45% | 30 | iPartiu |
| Samsung | Galaxy A52 | Bateria | Bateria original/compatível | 18 {{aprox.}} | 50 | 64% | 25 | iPartiu |
| Samsung | Galaxy A52 | Conector de carga | Flex USB-C | 12 {{aprox.}} | 45 | 73% | 45 | iPartiu |
| Samsung | Galaxy A53 5G | Ecrã | Ecrã original | 75 {{aprox.}} | 140 | 46% | 30 | iPartiu |
| Samsung | Galaxy A53 5G | Bateria | Bateria original/compatível | 22 {{aprox.}} | 70 | 69% | 25 | iPartiu |
| Samsung | Galaxy A53 5G | Conector de carga | Flex USB-C | 15 {{aprox.}} | 70 | 79% | 45 | iPartiu |
| Samsung | Galaxy S20 | Ecrã | Ecrã original AMOLED | 165 {{aprox.}} | 280 | 41% | 45 | iPartiu |
| Samsung | Galaxy S20 | Bateria | Bateria original/compatível | 30 {{aprox.}} | 90 | 67% | 30 | iPartiu |
| Samsung | Galaxy S20 | Conector de carga | Flex USB-C | 25 {{aprox.}} | 90 | 72% | 60 | iPartiu |
| Samsung | Galaxy S21 | Ecrã | Ecrã original AMOLED | 120 {{aprox.}} | 210 | 43% | 45 | iPartiu |
| Samsung | Galaxy S21 | Bateria | Bateria original/compatível | 25 {{aprox.}} | 65 | 62% | 30 | iPartiu |
| Samsung | Galaxy S21 | Conector de carga | Flex USB-C | 20 {{aprox.}} | 60 | 67% | 60 | iPartiu |
| Samsung | Galaxy S22 | Ecrã | Ecrã original AMOLED | 130 {{aprox.}} | 225 | 42% | 45 | iServices/iPartiu |
| Samsung | Galaxy S22 | Bateria | Bateria original/compatível | 25 {{aprox.}} | 62 | 60% | 30 | iServices/iPartiu |
| Samsung | Galaxy S22 | Conector de carga | Flex USB-C | 20 {{aprox.}} | 60 | 67% | 60 | iPartiu |
| Samsung | Galaxy S23 | Ecrã | Ecrã original AMOLED | 130 {{aprox.}} | 228 | 43% | 45 | iServices |
| Samsung | Galaxy S23 | Bateria | Bateria original/compatível | 25 {{aprox.}} | 55 | 55% | 30 | iServices |
| Samsung | Galaxy S23 | Conector de carga | Flex USB-C | 25 {{aprox.}} | 70 {{aprox.}} | 64% | 60 | iServices Contacte-nos + S22/S21 referência |
| Xiaomi | Redmi Note 10 | Ecrã | Ecrã original/compatível | 35 {{aprox.}} | 68 | 49% | 30 | iServices |
| Xiaomi | Redmi Note 10 | Bateria | Bateria compatível/premium | 15 {{aprox.}} | 45 | 67% | 25 | iServices |
| Xiaomi | Redmi Note 11 | Ecrã | Ecrã original Xiaomi | 55 {{aprox.}} | 111 | 50% | 30 | iServices |
| Xiaomi | Redmi Note 11 | Bateria | Bateria compatível/premium | 16 {{aprox.}} | 50 | 68% | 25 | iServices |
| Xiaomi | Redmi Note 12 | Ecrã | Ecrã original/compatível | 60 {{aprox.}} | 125 | 52% | 30 | iServices |
| Xiaomi | Redmi Note 12 | Bateria | Bateria compatível/premium | 16 {{aprox.}} | 45 {{aprox.}} | 64% | 25 | iServices Contacte-nos + Redmi Note 13 referência |
| Xiaomi | Mi 11 | Ecrã | Ecrã original Xiaomi | 130 {{aprox.}} | 226 | 42% | 45 | iServices |
| Xiaomi | Mi 11 | Bateria | Bateria compatível/premium | 18 {{aprox.}} | 45 | 60% | 25 | iServices |
| Xiaomi | Xiaomi 12/12 Pro | Ecrã | Ecrã AMOLED | 140 {{aprox.}} | 240 {{aprox.}} | 42% | 45 | iServices parcial + extrapolação |
| Xiaomi | Xiaomi 12/12 Pro | Bateria | Bateria compatível/premium | 20 {{aprox.}} | 50 {{aprox.}} | 60% | 25 | iServices 13T/Mi 11 referência |
| Xiaomi | Xiaomi 13 | Ecrã | Ecrã AMOLED | 80 {{aprox.}} | 152 | 47% | 45 | iServices |
| Xiaomi | Xiaomi 13 | Bateria | Bateria compatível/premium | 20 {{aprox.}} | 50 {{aprox.}} | 60% | 25 | iServices 13T referência |
| Huawei | P20 | Ecrã | Ecrã compatível/idêntico | 35 {{aprox.}} | 70 | 50% | 30 | iServices |
| Huawei | P20 | Bateria | Bateria compatível/premium | 15 {{aprox.}} | 45 | 67% | 25 | iServices |
| Huawei | P20 | Conector de carga | Flex USB-C | 15 {{aprox.}} | 80 | 81% | 45 | iServices |
| Huawei | P30 | Ecrã | Ecrã original Huawei | 60 {{aprox.}} | 117 | 49% | 30 | iServices |
| Huawei | P30 | Bateria | Bateria compatível/premium | 18 {{aprox.}} | 50 | 64% | 25 | iServices |
| Huawei | P30 Pro | Ecrã | Ecrã original Huawei | 80 {{aprox.}} | 148 | 46% | 45 | iServices |
| Huawei | P30 Pro | Bateria | Bateria compatível/premium | 18 {{aprox.}} | 50 | 64% | 25 | iServices |
| Huawei | P30 Pro | Conector de carga | Flex USB-C | 20 {{aprox.}} | 90 | 78% | 60 | iServices |
| Huawei | P40 Pro | Ecrã | Ecrã OLED | 110 {{aprox.}} | 197 | 44% | 45 | iServices |
| Huawei | P40 Pro | Câmara traseira | Módulo câmara traseira | 55 {{aprox.}} | 110 | 50% | 45 | iServices |
| Huawei | P40 Pro | Conector de carga | Flex USB-C | 25 {{aprox.}} | 110 | 77% | 60 | iServices |
| Huawei | Mate 20 Pro | Ecrã | Ecrã OLED | 160 {{aprox.}} | 225 | 29% | 45 | iServices/iPartiu/iLoja |
| Huawei | Mate 20 Pro | Bateria | Bateria compatível/premium | 18 {{aprox.}} | 52 | 65% | 25 | iServices/iPartiu/iLoja |
| Huawei | Mate 20 Pro | Conector de carga | Flex USB-C | 20 {{aprox.}} | 55 | 64% | 60 | iLoja |
| Apple | MacBook Air M1 13 2020 | Ecrã | Display assembly | 330 {{aprox.}} | 490 | 33% | 120 | iPartiu |
| Apple | MacBook Air M1 13 2020 | Bateria | Bateria | 60 {{aprox.}} | 105 | 43% | 90 | iPartiu |
| Apple | MacBook Air M1 13 2020 | Teclado | Top case/teclado | 90 {{aprox.}} | 160 | 44% | 180 | iPartiu |
| Apple | MacBook Air M1 13 2020 | SSD | Não aplicável soldado | 0 | 0 | 0% | 0 | técnico: SSD soldado, não sugerir como serviço normal |
| Apple | MacBook Air M2 13 2022 | Ecrã | Display assembly | 340 {{aprox.}} | 500 | 32% | 120 | Purplee |
| Apple | MacBook Air M2 13 2022 | Bateria | Bateria | 100 {{aprox.}} | 200 | 50% | 90 | Purplee |
| Apple | MacBook Air M2 13 2022 | Teclado | Top case/teclado | 150 {{aprox.}} | 260 | 42% | 180 | Purplee |
| Apple | MacBook Air M2 13 2022 | SSD | Não aplicável soldado | 0 | 0 | 0% | 0 | técnico: SSD soldado, não sugerir como serviço normal |
| PC Windows | Portátil genérico | SSD 500GB + instalação/clonagem | SSD SATA/NVMe 500GB | 45 {{aprox.}} | 120 {{aprox.}} | 63% | 60 | Purplee/PCFix |
| PC Windows | Portátil genérico | RAM 8GB + instalação | SODIMM DDR4/DDR5 | 35 {{aprox.}} | 80 {{aprox.}} | 56% | 30 | mercado PT {{aprox.}} |
| PC Windows | Portátil genérico | Ecrã 15.6 FHD | Painel LCD 15.6 | 65 {{aprox.}} | 150 {{aprox.}} | 57% | 60 | Purplee/PCFix |
| PC Windows | Portátil genérico | Teclado | Teclado PT/EN conforme modelo | 35 {{aprox.}} | 100 {{aprox.}} | 65% | 60 | Purplee |
| PC Windows | Portátil genérico | Bateria | Bateria compatível | 50 {{aprox.}} | 110 {{aprox.}} | 55% | 45 | Purplee |
| Acessórios | Universal | Película vidro temperado | Película 9H | 2 {{aprox.}} | 10 | 80% | 10 | mercado retalho PT {{aprox.}} |
| Acessórios | Universal | Película hidrogel | Película hidrogel | 3 {{aprox.}} | 15 | 80% | 10 | mercado retalho PT {{aprox.}} |
| Acessórios | Universal | Capa silicone/TPU | Capa compatível | 3 {{aprox.}} | 15 | 80% | 5 | mercado retalho PT {{aprox.}} |
| Acessórios | Universal | Carregador USB-C 20W | Carregador compatível | 8 {{aprox.}} | 20 | 60% | 5 | iPartiu acessórios/Baseus/Apple referência |
| Acessórios | Universal | Cabo USB-C/Lightning | Cabo compatível/certificado | 4 {{aprox.}} | 15 | 73% | 5 | iPartiu acessórios/Baseus/Apple referência |

## Notas por categoria

### iPhone 11-15

- O preço muda muito conforme a qualidade: compatível, recondicionado, original/Service Pack.
- Desde iPhone 11, baterias e ecrãs podem gerar mensagens de peça não genuína ou perda de funções se a reparação não for feita com processo/peça adequada.
- Para o RepairDesk, faz sentido ter 3 variantes de serviço:
  - `Ecrã compatível`
  - `Ecrã premium/recondicionado`
  - `Ecrã original/Service Pack`
- Vidro traseiro tem margem aparente alta, mas o risco técnico também é alto: laser, poeiras, dano em coils/câmaras, tempo e retrabalho.
- Para iPhone 15, os preços ainda devem ser revistos trimestralmente porque peças USB-C/ecrã estão a estabilizar.

### Samsung

- Samsung Galaxy S tem ecrãs caros e margem percentual mais baixa que acessórios/baterias.
- Série A é boa para oficina: PVP aceitável, menor risco e volume alto.
- A tabela deve permitir diferenciar `Original Samsung` vs `compatível`; no mercado PT, muitos clientes perguntam "fica igual ao original?".

### Xiaomi

- Redmi Note tem preço sensível. Se o ecrã passar dos 120-130 EUR, muitos clientes ponderam trocar de telemóvel.
- Xiaomi Mi/13 pode ter ecrãs caros, mas o valor usado do equipamento nem sempre justifica reparação. Mostrar alerta no RepairDesk quando PVP >40% do valor estimado usado.
- Em várias páginas iServices aparece `Contacte-nos`; nesses casos usei referência de modelos adjacentes e marquei `{{aprox.}}`.

### Huawei

- P20/P30 ainda aparecem muito em oficinas, mas stock pode ser irregular.
- Mate/P40 Pro têm ecrãs caros; margem bruta pode ser baixa se a oficina comprar peça boa.
- A Huawei tem serviço oficial de bateria em Portugal, por isso bateria Huawei deve ser competitiva e transparente.

### MacBooks

- MacBook Air/Pro M1/M2 não deve ter serviço "upgrade SSD" normal: armazenamento é soldado.
- Em Mac, o PVP parece alto, mas peças são caras, há risco e tempo de bancada. A margem percentual em ecrãs é menor do que em telemóveis gama média.
- Para oficinas pequenas, recomendação: só listar MacBook se houver capacidade técnica; caso contrário criar serviço `Diagnóstico MacBook` e subcontratar.

### PCs Windows

- O serviço mais vendável é SSD + instalação/clonagem. O cliente sente melhoria imediata.
- Ecrãs variam brutalmente por modelo: 15.6 FHD comum é barato; OLED/tátil/gaming/2K/4K sobe muito.
- RAM/SSD estão sujeitos a volatilidade de preço; rever mensalmente enquanto houver pressão global em memória/armazenamento.

### Acessórios

- São essenciais para margem e conveniência.
- Devem aparecer no RepairDesk como upsell pós-reparação: "Aplicar película?", "Adicionar capa?", "Trocar cabo/carregador?".
- Preço pode ser local: Viseu/interior pode vender película a 7,50-10 EUR; centros comerciais Lisboa/Porto frequentemente 12,90-19,90 EUR.

## Variação geográfica PT

| Zona | Ajuste recomendado no onboarding | Observação |
|---|---:|---|
| Lisboa/Porto/centros comerciais | +0% a +15% | Maior renda, mais tráfego, cliente aceita conveniência. |
| Cidades médias: Braga, Coimbra, Aveiro, Viseu, Leiria | -5% a -15% | Cliente compara mais, mas valoriza rapidez/local. |
| Interior pequeno | -10% a -25% | Menos volume, mas também menos concorrência; cuidado para não vender abaixo do custo real. |
| Serviço urgente/no próprio dia | +10% a +25% | Especialmente ecrãs iPhone/Samsung com stock. |
| Reparação por correio | +0% no PVP + portes | Melhor para lojas com volume e processo. |

Minha recomendação para Bruno/LopesTech em Viseu: começar com **-10% nos PVP de cadeia nacional**, exceto acessórios e serviços de baixo valor, onde a margem deve ser defendida.

## Como importar isto no produto

Campos sugeridos para a seed table:

```csv
marca,modelo,categoria,servico,peca,qualidade,custo_peca_eur,pvp_sugerido_eur,tempo_min,fonte,confidence
Apple,iPhone 12,Smartphone,Ecrã,OLED compatível/premium,premium,55,139,30,iServices/iLoja,media
Samsung,Galaxy A52,Smartphone,Bateria,Bateria compatível/premium,premium,18,50,25,iPartiu,media
PC Windows,Portátil genérico,Computador,SSD 500GB + instalação,SSD SATA/NVMe 500GB,standard,45,120,60,Purplee/PCFix,baixa
```

Regras no RepairDesk:

- Guardar `preco_sugerido` separado de `preco_tenant`.
- Mostrar etiqueta "preço de referência" até a loja editar.
- Guardar `fonte_preco` e `data_referencia`.
- Permitir duplicar serviço por qualidade da peça.
- Alertar quando `pvp < custo_peca * 1.35`, porque fica pouca folga para garantia/IVA/retrabalho.

## Fontes utilizadas

- iServices, iPhone 12: https://iservices.pt/reparacao/apple/iphone/iphone-12-e-12-mini/iphone-12
- iServices, iPhone 13: https://iservices.pt/reparacao/apple/iphone/iphone-13-e-13-mini/iphone-13
- iServices, iPhone 14: https://iservices.pt/reparacao/apple/iphone/iphone-14-14-plus/iphone-14
- iServices, iPhone 15: https://iservices.pt/reparacao/apple/iphone/iphone-15-15-plus/iphone-15
- iLoja, iPhone 11: https://www.iloja.pt/reparacoes/apple/iphone/iphone-11/
- iPartiu, iPhone 11/13/14/15: https://www.ipartiu.pt/produto/reparacao-iphone-11/ ; https://www.ipartiu.pt/produto/reparacao-iphone-13/ ; https://www.ipartiu.pt/produto/reparacao-iphone-14/ ; https://www.ipartiu.pt/produto/reparacao-iphone-15/
- iServices, Samsung A50/S22/S23: https://iservices.pt/reparacao/samsung/serie-a/samsung-galaxy-a50 ; https://iservices.pt/reparacao/samsung/samsung-s22/problema/vidro-ecra-touch-smartphone ; https://iservices.pt/reparacao/samsung/serie-galaxy-s/samsung-galaxy-s23
- iPartiu, Samsung A52/A53/S20/S21/S22: https://www.ipartiu.pt/produto/reparacao-galaxy-a52-4g/ ; https://www.ipartiu.pt/produto/reparacao-galaxy-a53/ ; https://www.ipartiu.pt/produto/reparacao-galaxy-s20/ ; https://www.ipartiu.pt/produto/reparacao-galaxy-s21/ ; https://www.ipartiu.pt/produto/reparacao-galaxy-s22/
- iServices, Xiaomi Redmi/Mi/Xiaomi 13: https://iservices.pt/reparacao/xiaomi/serie-redmi/xiaomi-redmi-note-10-5g ; https://iservices.pt/reparacao/xiaomi/serie-redmi/xiaomi-redmi-note-11 ; https://iservices.pt/reparacao/xiaomi/serie-redmi/xiaomi-redmi-note-12 ; https://iservices.pt/reparacao/xiaomi/serie-mi/xiaomi-mi-11 ; https://iservices.pt/reparacao/xiaomi/produto/xiaomi-13 ; https://iservices.pt/reparacao/xiaomi/produto/xiaomi-13t
- iServices, Huawei P/Mate: https://iservices.pt/reparacao/huawei/serie-p/huawei-p20 ; https://iservices.pt/reparacao/huawei/serie-p/huawei-p30 ; https://iservices.pt/reparacao/huawei/serie-p/huawei-p30-pro ; https://iservices.pt/reparacao/huawei/serie-p/huawei-p40-pro ; https://iservices.pt/reparacao/huawei/serie-mate/huawei-mate-20-pro
- iPartiu/iLoja, Huawei Mate 20 Pro: https://www.ipartiu.pt/produto/reparacao-huawei-mate-20-pro/ ; https://www.iloja.pt/reparacoes/huawei/mate/mate-20-pro/
- iPartiu, MacBook Air M1: https://www.ipartiu.pt/produto/reparacao-macbook-air-13-m1-2020/
- Purplee, MacBook Air M2 e PC Windows: https://purplee.pt/pages/reparacao-apple-macbook-air-m2-2022 ; https://purplee.pt/pages/reparacao-computador-hardware
- PCFix, SSD portátil: https://pcfix.com.pt/
- ReparaJá, peça vidro traseiro iPhone 12: https://reparaja.pt/produto/vidro-traseiro-tampa-de-bateria-c-logo-iphone-12/
- Microwire, peça ecrã Redmi Note 10/10S: https://www.microwire.pt/xiaomi-redmi-note-10-10s-ecra-tatil-e-moldura-oled
- Reddit PT, sensibilidade a preço/qualidade: https://www.reddit.com/r/braga/comments/1mh8lpy/ ; https://www.reddit.com/r/portugal/comments/1kbbvg3/ ; https://www.reddit.com/r/TecnologiaPT/comments/1rnd1xs/trocar_bateria_do_iphone_original_ou_compat%C3%ADvel/
