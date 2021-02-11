using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sample_GrpcService.Models
{
    /// <summary>
    /// 開業日
    /// </summary>
    public class OpeningDays
    {
        //開業時刻
        public int OpeningTime { get; set; }
        //終業時刻
        public int ClosingTime { get; set; }
        //開業週
        public DayOfWeek OpeningWeek { get; set; }

        //コンストラクタ
        public OpeningDays(int openingTime, int closingTime, DayOfWeek dayOfWeek)
        {
            this.OpeningTime = openingTime;
            this.ClosingTime = closingTime;
            this.OpeningWeek = dayOfWeek;
        }
    }
}
