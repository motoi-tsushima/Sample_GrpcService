using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sample_GrpcService.Models;
using System.IO;

namespace Sample_GrpcService
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }

        /// <summary>
        /// 単項メソッドの追加テスト
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<MyFunctionRply> MyFunction(MyRequest request, ServerCallContext context)
        {
            return Task.FromResult(new MyFunctionRply
            {
                Message = "Reply ! " + request.Parameter1 + "=" + request.ParameterIntValue
            });
        }

        /// <summary>
        /// 四則演算
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<CalcResult> Calc(CalcParameter parameter, ServerCallContext context)
        {
            Int32 value1 = parameter.Parameter1;
            Int32 value2 = parameter.Parameter2;

            return Task.FromResult(new CalcResult
            {
                Addition = value1 + value2,
                Subtraction = value1 - value2,
                Multiplication = value1 * value2,
                Division = value1 / value2
            });
        }

        /// <summary>
        /// 時差の変更
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ReservationTime> ChangeTimeZone(ReservationTime request, ServerCallContext context)
        {
            //リクエストパラメータを取得する
            DateTime requestDateTime = request.Time.ToDateTime();
            TimeSpan timeZone = request.TimeZone.ToTimeSpan();

            //時差を変更する
            DateTime changeDateTime = requestDateTime.Add(timeZone);

            //時差を変更した日時を設定する。
            ReservationTime reservation = new ReservationTime();
            reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(changeDateTime);
            reservation.TimeZone = request.TimeZone;

            //時差を変更した日時を返す。
            return Task.FromResult(reservation);
        }

        /// <summary>
        /// 施設予約
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ReservationTime> Reserve(ReservationTime request, ServerCallContext context)
        {
            //リクエストパラメータを取得する
            DateTimeOffset requestDateTimeUtc = request.Time.ToDateTimeOffset();
            //TimeSpan requestTimeZone = request.TimeZone.ToTimeSpan();
            TimeSpan requestTimeZone = TimeSpan.Zero;
            TimeSpan requestCountryTimeZone = request.CountryTimeZone.ToTimeSpan();

            DateTimeOffset requestDateTimeOffset = 
                new DateTimeOffset(requestDateTimeUtc.DateTime, requestTimeZone);
            DateTimeOffset requestCountryDateTimeOffset = 
                new DateTimeOffset(requestDateTimeUtc.ToOffset(requestCountryTimeZone).DateTime, requestCountryTimeZone);

            TimeSpan requestTimeSpan = request.Duration.ToTimeSpan();

            //日程調整
            requestCountryDateTimeOffset = ScheduleAdjustment(requestCountryDateTimeOffset, requestTimeSpan);

            //予約日時を設定する。
            ReservationTime reservation = new ReservationTime();
            reservation.Subject = request.Subject;
            //reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(requestDateTimeOffset);
            reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(requestCountryDateTimeOffset);
            reservation.Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(requestTimeSpan);
            reservation.TimeZone = request.TimeZone;
            reservation.CountryTimeZone = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(requestCountryTimeZone);

            //予約日時を返す。
            return Task.FromResult(reservation);
        }

        /// <summary>
        /// 日程調整
        /// </summary>
        /// <param name="requestCountryDateTimeOffset">施設側タイムゾーンの日時</param>
        /// <param name="requestTimeSpan">予約時間</param>
        /// <returns></returns>
        private DateTimeOffset ScheduleAdjustment(DateTimeOffset requestCountryDateTimeOffset, TimeSpan requestTimeSpan)
        {
            //開業時間と曜日
            List<OpeningDays> openingDays = new List<OpeningDays> {
                new OpeningDays( 9, 17, DayOfWeek.Monday )
                ,new OpeningDays( 9, 17, DayOfWeek.Tuesday )
                ,new OpeningDays( 9, 12, DayOfWeek.Wednesday )
                ,new OpeningDays( 9, 17, DayOfWeek.Thursday )
                ,new OpeningDays( 13, 20, DayOfWeek.Friday )
            };

            int whereCount;
            OpeningDays selectedDays = openingDays.FirstOrDefault();
            bool addDayProcess = false;

            //リクエスト日より後の開業曜日を検索する。
            do
            {
                List<OpeningDays> openings =
                    openingDays.Where(d => d.OpeningWeek == requestCountryDateTimeOffset.DayOfWeek)
                    .ToList<OpeningDays>();
                whereCount = openings.Count();

                if (whereCount == 0)
                {
                    //開業曜日に含まれない場合、一日追加する。
                    requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddDays(1.0);
                }
                else if (addDayProcess == true)
                {
                    selectedDays = openings.FirstOrDefault();

                    //リクエスト日の終了時間が遅すぎる場合、営業時間の最も遅い時間に、時間を早める。
                    TimeSpan subtractionTime = new TimeSpan(
                        selectedDays.ClosingTime - requestCountryDateTimeOffset.Hour - requestTimeSpan.Hours, 
                        (requestTimeSpan.Minutes + requestCountryDateTimeOffset.Minute) * (-1) , 0);

                    requestCountryDateTimeOffset = requestCountryDateTimeOffset.Add(subtractionTime);

                    addDayProcess = false;
                }
                else
                {
                    //開業曜日に含まれる場合、該当の開業時間と曜日を返す。
                    selectedDays = openings.FirstOrDefault();

                    //リクエスト日の終了時間が遅すぎる場合、時間を早めて、一日追加する。
                    double countryMinutes = (double)requestCountryDateTimeOffset.Minute / 60.0;
                    double durationMinutes = ((double)requestTimeSpan.Hours * 60.0 + (double)requestTimeSpan.Minutes) / 60.0;

                    if ((double)selectedDays.ClosingTime <
                        ((double)requestCountryDateTimeOffset.Hour + countryMinutes + durationMinutes)
                        )
                    {
                        requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddDays(1.0);

                        addDayProcess = true;
                        whereCount = 0;
                    }
                }
            }
            while (whereCount == 0);


            //リクエスト日の開始時間が早すぎる場合、開始時間に変更する。
            if (requestCountryDateTimeOffset.Hour < selectedDays.OpeningTime)
            {
                requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddHours(selectedDays.OpeningTime - requestCountryDateTimeOffset.Hour);
                requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddMinutes(requestCountryDateTimeOffset.Minute * -1);
            }

            return requestCountryDateTimeOffset;
        }

        /// <summary>
        /// ファイルダウンロード(サーバー ストリーミング メソッド)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="responseStream">ストリーミング</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task FileDownload(FileDownloadRequest request,
            IServerStreamWriter<FileDownloadStream> responseStream, ServerCallContext context)
        {
            const int BufferSize = 10240;
            byte[] buffer = new byte[BufferSize];

            string currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine("$CurrentDirectory = {0}", currentDir);


            using(var fs = new FileStream(request.FileName, FileMode.Open, FileAccess.Read))
            {
                int downloadSize = 0;
                int readSize = 0;

                while ((readSize = fs.Read(buffer, 0, BufferSize)) > 0)
                {
                    Console.WriteLine("ダウンロード リクエスト");

                    //クライアントからキャンセルされたら終了する。
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("キャンセル リクエスト");
                        break;
                    }

                    FileDownloadStream fileDownloadStream = new FileDownloadStream();
                    fileDownloadStream.Binary = Google.Protobuf.ByteString.CopyFrom(buffer);
                    fileDownloadStream.FileName = request.FileName;
                    fileDownloadStream.FileSize = readSize;

                    await responseStream.WriteAsync(fileDownloadStream);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    //await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);

                    downloadSize += readSize;

                    Console.WriteLine("{0}byte ダウンロード", downloadSize);
                }
            }
        }
    }
}
