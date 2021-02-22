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
        /// �P�����\�b�h�̒ǉ��e�X�g
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
        /// �l�����Z
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
        /// �����̕ύX
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ReservationTime> ChangeTimeZone(ReservationTime request, ServerCallContext context)
        {
            //���N�G�X�g�p�����[�^���擾����
            DateTime requestDateTime = request.Time.ToDateTime();
            TimeSpan timeZone = request.TimeZone.ToTimeSpan();

            //������ύX����
            DateTime changeDateTime = requestDateTime.Add(timeZone);

            //������ύX����������ݒ肷��B
            ReservationTime reservation = new ReservationTime();
            reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(changeDateTime);
            reservation.TimeZone = request.TimeZone;

            //������ύX����������Ԃ��B
            return Task.FromResult(reservation);
        }

        /// <summary>
        /// �{�ݗ\��
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<ReservationTime> Reserve(ReservationTime request, ServerCallContext context)
        {
            //���N�G�X�g�p�����[�^���擾����
            DateTimeOffset requestDateTimeUtc = request.Time.ToDateTimeOffset();
            //TimeSpan requestTimeZone = request.TimeZone.ToTimeSpan();
            TimeSpan requestTimeZone = TimeSpan.Zero;
            TimeSpan requestCountryTimeZone = request.CountryTimeZone.ToTimeSpan();

            DateTimeOffset requestDateTimeOffset = 
                new DateTimeOffset(requestDateTimeUtc.DateTime, requestTimeZone);
            DateTimeOffset requestCountryDateTimeOffset = 
                new DateTimeOffset(requestDateTimeUtc.ToOffset(requestCountryTimeZone).DateTime, requestCountryTimeZone);

            TimeSpan requestTimeSpan = request.Duration.ToTimeSpan();

            //��������
            requestCountryDateTimeOffset = ScheduleAdjustment(requestCountryDateTimeOffset, requestTimeSpan);

            //�\�������ݒ肷��B
            ReservationTime reservation = new ReservationTime();
            reservation.Subject = request.Subject;
            //reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(requestDateTimeOffset);
            reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(requestCountryDateTimeOffset);
            reservation.Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(requestTimeSpan);
            reservation.TimeZone = request.TimeZone;
            reservation.CountryTimeZone = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(requestCountryTimeZone);

            //�\�������Ԃ��B
            return Task.FromResult(reservation);
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="requestCountryDateTimeOffset">�{�ݑ��^�C���]�[���̓���</param>
        /// <param name="requestTimeSpan">�\�񎞊�</param>
        /// <returns></returns>
        private DateTimeOffset ScheduleAdjustment(DateTimeOffset requestCountryDateTimeOffset, TimeSpan requestTimeSpan)
        {
            //�J�Ǝ��ԂƗj��
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

            //���N�G�X�g������̊J�Ɨj������������B
            do
            {
                List<OpeningDays> openings =
                    openingDays.Where(d => d.OpeningWeek == requestCountryDateTimeOffset.DayOfWeek)
                    .ToList<OpeningDays>();
                whereCount = openings.Count();

                if (whereCount == 0)
                {
                    //�J�Ɨj���Ɋ܂܂�Ȃ��ꍇ�A����ǉ�����B
                    requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddDays(1.0);
                }
                else if (addDayProcess == true)
                {
                    selectedDays = openings.FirstOrDefault();

                    //���N�G�X�g���̏I�����Ԃ��x������ꍇ�A�c�Ǝ��Ԃ̍ł��x�����ԂɁA���Ԃ𑁂߂�B
                    TimeSpan subtractionTime = new TimeSpan(
                        selectedDays.ClosingTime - requestCountryDateTimeOffset.Hour - requestTimeSpan.Hours, 
                        (requestTimeSpan.Minutes + requestCountryDateTimeOffset.Minute) * (-1) , 0);

                    requestCountryDateTimeOffset = requestCountryDateTimeOffset.Add(subtractionTime);

                    addDayProcess = false;
                }
                else
                {
                    //�J�Ɨj���Ɋ܂܂��ꍇ�A�Y���̊J�Ǝ��ԂƗj����Ԃ��B
                    selectedDays = openings.FirstOrDefault();

                    //���N�G�X�g���̏I�����Ԃ��x������ꍇ�A���Ԃ𑁂߂āA����ǉ�����B
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


            //���N�G�X�g���̊J�n���Ԃ���������ꍇ�A�J�n���ԂɕύX����B
            if (requestCountryDateTimeOffset.Hour < selectedDays.OpeningTime)
            {
                requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddHours(selectedDays.OpeningTime - requestCountryDateTimeOffset.Hour);
                requestCountryDateTimeOffset = requestCountryDateTimeOffset.AddMinutes(requestCountryDateTimeOffset.Minute * -1);
            }

            return requestCountryDateTimeOffset;
        }

        /// <summary>
        /// �t�@�C���_�E�����[�h(�T�[�o�[ �X�g���[�~���O ���\�b�h)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="responseStream">�X�g���[�~���O</param>
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
                    Console.WriteLine("�_�E�����[�h ���N�G�X�g");

                    //�N���C�A���g����L�����Z�����ꂽ��I������B
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("�L�����Z�� ���N�G�X�g");
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

                    Console.WriteLine("{0}byte �_�E�����[�h", downloadSize);
                }
            }
        }
    }
}
