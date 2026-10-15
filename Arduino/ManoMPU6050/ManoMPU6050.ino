/*
  ManoMPU6050.ino
  Lee el MPU6050 y envía la orientación por el puerto serial a Unity.

  Conexiones (Arduino UNO / Nano):
    MPU6050 VCC -> 5V   (o 3.3V)
    MPU6050 GND -> GND
    MPU6050 SDA -> A4
    MPU6050 SCL -> A5
  (En Arduino MEGA: SDA -> 20, SCL -> 21)

  Formato que se envía (una línea, ~50 veces por segundo):
    pitch,roll,yaw      en grados, por ejemplo:  12.35,-4.10,87.02

  No necesita librerías extra, solo Wire (viene con Arduino).
  IMPORTANTE: al encender, deja el sensor QUIETO ~2 segundos para que se calibre.
*/

#include <Wire.h>

const uint8_t MPU_ADDR = 0x68;   // 0x69 si el pin AD0 está a 5V
const float ALFA = 0.96;          // filtro complementario (0.9 - 0.98)
const unsigned long PERIODO_MS = 20; // 50 Hz

float offsetGx = 0, offsetGy = 0, offsetGz = 0;
float pitch = 0, roll = 0, yaw = 0;
unsigned long tAnterior = 0;
unsigned long tEnvio = 0;

void escribirRegistro(uint8_t reg, uint8_t valor) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(reg);
  Wire.write(valor);
  Wire.endTransmission();
}

void leerCrudo(int16_t &ax, int16_t &ay, int16_t &az,
               int16_t &gx, int16_t &gy, int16_t &gz) {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);               // ACCEL_XOUT_H
  Wire.endTransmission(false);
  Wire.requestFrom(MPU_ADDR, (uint8_t)14);
  ax = (Wire.read() << 8) | Wire.read();
  ay = (Wire.read() << 8) | Wire.read();
  az = (Wire.read() << 8) | Wire.read();
  Wire.read(); Wire.read();       // temperatura (no se usa)
  gx = (Wire.read() << 8) | Wire.read();
  gy = (Wire.read() << 8) | Wire.read();
  gz = (Wire.read() << 8) | Wire.read();
}

void calibrarGiroscopio() {
  const int MUESTRAS = 500;
  long sx = 0, sy = 0, sz = 0;
  int16_t ax, ay, az, gx, gy, gz;
  for (int i = 0; i < MUESTRAS; i++) {
    leerCrudo(ax, ay, az, gx, gy, gz);
    sx += gx; sy += gy; sz += gz;
    delay(3);
  }
  offsetGx = sx / (float)MUESTRAS;
  offsetGy = sy / (float)MUESTRAS;
  offsetGz = sz / (float)MUESTRAS;
}

void setup() {
  Serial.begin(115200);
  Wire.begin();
  Wire.setClock(400000);

  escribirRegistro(0x6B, 0x00);   // PWR_MGMT_1: despertar el sensor
  escribirRegistro(0x1A, 0x03);   // CONFIG: filtro pasa-bajas ~44 Hz
  escribirRegistro(0x1B, 0x00);   // GYRO_CONFIG: ±250 °/s  (131 LSB por °/s)
  escribirRegistro(0x1C, 0x00);   // ACCEL_CONFIG: ±2 g     (16384 LSB por g)

  // Comprobar que el sensor responde
  Wire.beginTransmission(MPU_ADDR);
  if (Wire.endTransmission() != 0) {
    while (true) {
      Serial.println("ERROR: no se encuentra el MPU6050, revisa SDA/SCL");
      delay(1000);
    }
  }

  Serial.println("Calibrando... no muevas el sensor");
  calibrarGiroscopio();

  // Ángulos iniciales a partir del acelerómetro
  int16_t ax, ay, az, gx, gy, gz;
  leerCrudo(ax, ay, az, gx, gy, gz);
  pitch = atan2(ay, sqrt((float)ax * ax + (float)az * az)) * RAD_TO_DEG;
  roll  = atan2(-ax, az) * RAD_TO_DEG;
  yaw   = 0;

  Serial.println("LISTO");
  tAnterior = micros();
}

void loop() {
  int16_t ax, ay, az, gx, gy, gz;
  leerCrudo(ax, ay, az, gx, gy, gz);

  unsigned long ahora = micros();
  float dt = (ahora - tAnterior) / 1000000.0;
  tAnterior = ahora;

  // Velocidad angular en °/s
  float gxs = (gx - offsetGx) / 131.0;
  float gys = (gy - offsetGy) / 131.0;
  float gzs = (gz - offsetGz) / 131.0;

  // Ángulos según el acelerómetro (solo sirven para pitch y roll)
  float pitchAcc = atan2(ay, sqrt((float)ax * ax + (float)az * az)) * RAD_TO_DEG;
  float rollAcc  = atan2(-ax, az) * RAD_TO_DEG;

  // Filtro complementario: giroscopio rápido + acelerómetro estable
  pitch = ALFA * (pitch + gxs * dt) + (1.0 - ALFA) * pitchAcc;
  roll  = ALFA * (roll  + gys * dt) + (1.0 - ALFA) * rollAcc;

  // Yaw solo con giroscopio (sin magnetómetro se desvía poco a poco)
  if (fabs(gzs) > 0.5) yaw += gzs * dt;   // zona muerta para reducir deriva

  if (millis() - tEnvio >= PERIODO_MS) {
    tEnvio = millis();
    Serial.print(pitch, 2); Serial.print(',');
    Serial.print(roll, 2);  Serial.print(',');
    Serial.println(yaw, 2);
  }

  // Si Unity manda 'r', se reinicia el yaw a 0
  if (Serial.available()) {
    char c = Serial.read();
    if (c == 'r' || c == 'R') yaw = 0;
  }
}
