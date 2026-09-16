// NavBR Hardware Cockpit - exemplo minimo USB/Serial
// Protocolo: NAVBR_HW_V1, JSON Lines, 115200 baud, ~5 Hz.
//
// Este exemplo funciona sem bibliotecas adicionais. Ele recebe uma linha JSON
// por quadro de telemetria e acende LED_BUILTIN quando stopRequested=true.
// Para displays OLED/LCD/matriz, use os mesmos campos do JSON como entrada.

#include <Arduino.h>
#include <string.h>

constexpr unsigned long NAVBR_BAUD = 115200;
constexpr size_t FRAME_BUFFER_SIZE = 768;

char frameBuffer[FRAME_BUFFER_SIZE];
size_t frameLength = 0;

bool containsToken(const char* frame, const char* token) {
  return strstr(frame, token) != nullptr;
}

void applyNavBRFrame(const char* frame) {
  // Ignora qualquer outra linha que eventualmente chegue pela porta.
  if (!containsToken(frame, "\"protocol\":\"NAVBR_HW_V1\"")) {
    return;
  }

  const bool stopRequested =
      containsToken(frame, "\"stopRequested\":true");

  digitalWrite(LED_BUILTIN, stopRequested ? HIGH : LOW);
}

void setup() {
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
  Serial.begin(NAVBR_BAUD);
}

void loop() {
  while (Serial.available() > 0) {
    const char ch = static_cast<char>(Serial.read());

    if (ch == '\r') {
      continue;
    }

    if (ch == '\n') {
      if (frameLength > 0) {
        frameBuffer[frameLength] = '\0';
        applyNavBRFrame(frameBuffer);
        frameLength = 0;
      }
      continue;
    }

    if (frameLength < FRAME_BUFFER_SIZE - 1) {
      frameBuffer[frameLength++] = ch;
    } else {
      // Quadro incompleto/grande demais: descarta ate o proximo newline.
      frameLength = 0;
    }
  }
}
