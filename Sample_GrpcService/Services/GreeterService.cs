using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sample_GrpcService.Models;

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

        public override Task<MyFunctionRply> MyFunction(MyRequest request, ServerCallContext context)
        {
            return Task.FromResult(new MyFunctionRply
            {
                Message = "Reply ! " + request.Parameter1 + "=" + request.ParameterIntValue
            });
        }

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

        public override Task<ReservationTime> Reserve(ReservationTime request, ServerCallContext context)
        {
            TimeSpan JapanTimeSpan = new TimeSpan(9, 0, 0);
            TimeSpan BritainTimeSpan = new TimeSpan(0, 0, 0);
            TimeSpan AmericaEasternTimeSpan = new TimeSpan(-4, 0, 0);


            //�^�C���]�[����ݒ肷��
            TimeSpan timeSpan;

            switch (request.TimeZone.Trim())
            {
                case "Brirain":
                    timeSpan = BritainTimeSpan;
                    break;
                case "Japan":
                    timeSpan = JapanTimeSpan;
                    break;
                case "AmericaEastern":
                    timeSpan = AmericaEasternTimeSpan;
                    break;
                default:
                    timeSpan = BritainTimeSpan;
                    break;
            }

            //���N�G�X�g�p�����[�^���擾����
            DateTimeOffset requestDateTime = request.Time.ToDateTimeOffset();
            TimeSpan requestTimeSpan = request.Duration.ToTimeSpan();

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

            //���N�G�X�g������̊J�Ɨj������������B
            do
            {
                List<OpeningDays>  openings = openingDays.Where(d => d.OpeningWeek == requestDateTime.DayOfWeek).ToList<OpeningDays>();
                whereCount = openings.Count();

                if(whereCount == 0)
                {
                    //�J�Ɨj���Ɋ܂܂�Ȃ��ꍇ�A����ǉ�����B
                    requestDateTime = requestDateTime.AddDays(1.0);
                }
                else
                {
                    //�J�Ɨj���Ɋ܂܂��ꍇ�A�Y���̊J�Ǝ��ԂƗj����Ԃ��B
                    selectedDays = openings.FirstOrDefault();
                }
            }
            while (whereCount == 0);


            //���N�G�X�g���̊J�n���Ԃ���������ꍇ�A�J�n���ԂɕύX����B
            if (requestDateTime.Hour < selectedDays.OpeningTime)
            {
                requestDateTime = requestDateTime.AddHours(selectedDays.OpeningTime - requestDateTime.Hour);
                requestDateTime = requestDateTime.AddMinutes(requestDateTime.Minute * -1);
            }

            //���N�G�X�g���̏I�����Ԃ��x������ꍇ�A�I�����ԂɕύX����B
            if (selectedDays.ClosingTime < (requestDateTime.Hour + requestTimeSpan.Hours))
            {
                requestDateTime = requestDateTime.AddHours(
                    (requestDateTime.Hour - selectedDays.ClosingTime + requestTimeSpan.Hours) * -1
                    );
            }

            //�\�������ݒ肷��B
            ReservationTime reservation = new ReservationTime();
            reservation.Subject = request.Subject;
            reservation.Time = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(requestDateTime);
            reservation.Duration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(requestTimeSpan);
            reservation.TimeZone = request.TimeZone;

            //�\�������Ԃ��B
            return Task.FromResult(reservation);
        }

    }
}
