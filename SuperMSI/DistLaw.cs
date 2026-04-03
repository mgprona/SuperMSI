using System;

namespace SuperMSI
{
    /// <summary>
    /// คลาส DistLaw (กฎของระยะ) 
    /// ใช้สำหรับตรวจสอบเกณฑ์ความถูกต้องของระยะรอบแปลงตามมาตรฐาน
    /// </summary>
    public static class DistLaw
    {
        /// <summary>
        /// ฟังก์ชันคำนวณกฎของระยะ (distlaw)
        /// </summary>
        /// <param name="distance">ระยะที่วัดได้จริง (เมตร)</param>
        /// <returns>ค่าความคลาดเคลื่อนสูงสุดที่ยอมรับได้ (เมตร) ทศนิยม 3 ตำแหน่ง</returns>
        public static double Calculate(double distance)
        {
            // ป้องกันข้อผิดพลาดกรณีค่าที่ส่งมาติดลบ
            if (distance < 0) return 0;

            // คำนวณตามโมเดลทางคณิตศาสตร์
            double term1 = 0.00095 * Math.Sqrt(distance / 40.0);
            double term2 = 0.00035 * (distance / 40.0);
            double term3 = 0.0005;

            // รวมค่าและคูณด้วย 40
            double distlawValue = (term1 + term2 + term3) * 40.0;

            // ปัดเศษให้เป็นทศนิยม 3 ตำแหน่ง เพื่อใช้เป็นเกณฑ์ตัดสิน
            return Math.Round(distlawValue, 3, MidpointRounding.AwayFromZero);
        }
    }
}