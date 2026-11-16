/*
  BrazoBNO085.ino  (Arduino Mega + TCA9548A + 3x BNO085)

  Manda a Unity la orientación de los 3 sensores y la presión de 2 FSR:
    Q,w0,x0,y0,z0,w1,x1,y1,z1,w2,x2,y2,z2,f0,f1
  Sensor 0 = hombro (brazo), 1 = codo (antebrazo), 2 = muñeca (mano).
  f0 y f1 = presión de los FSR (0 = nada, 1023 = máxima).
  Si un sensor está desconectado manda 0,0,0,0 en su lugar (Unity lo ignora).
  También manda líneas que empiezan con '#' con avisos (Unity las ignora).

  Conexiones: igual que en las pruebas
    TCA9548A: VIN->5V, GND->GND, SDA->SDA, SCL->SCL, A0/A1/A2->GND, RST->VIN
    Hombro -> canal 0 (SD0/SC0), Codo -> canal 1, Muñeca -> canal 2

  FSR402 (cada uno igual):
    5V --- FSR --- pin A0 (o A1) --- resistencia 2 kΩ (dos de 1 kΩ en serie) --- GND

  Librería: "SparkFun BNO080 Cortex Based IMU"
*/

#include <Wire.h>
#include "SparkFun_BNO080_Arduino_Library.h"

// true  = Game Rotation Vector (sin magnetómetro): no le afectan metales ni cables,
//         el giro horizontal se desvía muy poco a poco (se corrige recalibrando).
// false = Rotation Vector (con magnetómetro): sin desvío, pero sensible a metales.
const bool USAR_GAME_ROTATION = true;

const uint8_t TCA = 0x70;
const uint8_t CANAL[3] = {0, 1, 2};
const unsigned long PERIODO_ENVIO_MS = 20;   // 50 veces por segundo
const uint8_t PIN_FSR[2] = {A0, A1};

BNO080 imu[3];
bool activo[3] = {false, false, false};
float qw[3], qx[3], qy[3], qz[3];
unsigned long ultimoDato[3] = {0, 0, 0};
unsigned long ultimoIntento[3] = {0, 0, 0};
unsigned long tEnvio = 0;

void elegirCanal(int c) {
  Wire.beginTransmission(TCA);
  Wire.write(c < 0 ? 0 : (1 << c));   // -1 = cerrar todos
  Wire.endTransmission();
}

bool existe(uint8_t dir) {
  Wire.beginTransmission(dir);
  return Wire.endTransmission() == 0;
}

bool iniciarSensor(int i) {
  elegirCanal(CANAL[i]);
  delay(10);
  uint8_t dir = existe(0x4A) ? 0x4A : (existe(0x4B) ? 0x4B : 0);
  bool ok = false;
  if (dir != 0 && imu[i].begin(dir, Wire)) {
    if (USAR_GAME_ROTATION) imu[i].enableGameRotationVector(20);
    else                    imu[i].enableRotationVector(20);
    ok = true;
  }
  elegirCanal(-1);
  return ok;
}

void setup() {
  Serial.begin(115200);
  delay(1000);
  Wire.begin();
  Wire.setWireTimeout(25000, true);
  elegirCanal(-1);

  for (int i = 0; i < 3; i++) {
    activo[i] = iniciarSensor(i);
    ultimoDato[i] = millis();
    qw[i] = qx[i] = qy[i] = qz[i] = 0;
    Serial.print("# Sensor "); Serial.print(i);
    Serial.println(activo[i] ? " OK" : " FALLO (reintentando)");
  }
  Serial.println("# LISTO");
}

void loop() {
  for (int i = 0; i < 3; i++) {
    if (activo[i]) {
      elegirCanal(CANAL[i]);
      if (imu[i].dataAvailable()) {
        qw[i] = imu[i].getQuatReal();
        qx[i] = imu[i].getQuatI();
        qy[i] = imu[i].getQuatJ();
        qz[i] = imu[i].getQuatK();
        ultimoDato[i] = millis();
      }
      elegirCanal(-1);

      if (millis() - ultimoDato[i] > 1000) {
        activo[i] = false;
        qw[i] = qx[i] = qy[i] = qz[i] = 0;
        Serial.print("# Sensor "); Serial.print(i); Serial.println(" desconectado");
      }
    } else if (millis() - ultimoIntento[i] > 1000) {
      ultimoIntento[i] = millis();
      if (iniciarSensor(i)) {
        activo[i] = true;
        ultimoDato[i] = millis();
        Serial.print("# Sensor "); Serial.print(i); Serial.println(" reconectado");
      }
    }
  }

  if (millis() - tEnvio >= PERIODO_ENVIO_MS) {
    tEnvio = millis();
    Serial.print('Q');
    for (int i = 0; i < 3; i++) {
      Serial.print(','); Serial.print(qw[i], 4);
      Serial.print(','); Serial.print(qx[i], 4);
      Serial.print(','); Serial.print(qy[i], 4);
      Serial.print(','); Serial.print(qz[i], 4);
    }
    for (int f = 0; f < 2; f++) {
      Serial.print(','); Serial.print(analogRead(PIN_FSR[f]));
    }
    Serial.println();
  }
}
