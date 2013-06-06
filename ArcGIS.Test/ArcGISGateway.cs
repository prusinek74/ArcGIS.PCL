﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.ServiceModel.Common;
using ArcGIS.ServiceModel.Extensions;
using ArcGIS.ServiceModel.Logic;
using ArcGIS.ServiceModel.Operation;
using ServiceStack.Text;
using Xunit;

namespace ArcGIS.Test
{
    public class ArcGISGateway : PortalGateway
    {
        public ArcGISGateway()
            : this(@"http://sampleserver3.arcgisonline.com/ArcGIS/", String.Empty, String.Empty)
        { }

        public ArcGISGateway(String root, String username, String password)
            : base(root, username, password)
        {
            Serializer = new Serializer();
        }

        public async Task<List<Feature<T>>> Query<T>(Query queryOptions) where T : IGeometry
        {
            var result = await Post<QueryResponse<T>, Query>(queryOptions.RelativeUrl, queryOptions);
            return result.Features.ToList();
        }
    }

    public class SecureGISGateway : ArcGISGateway
    {
        public SecureGISGateway()
            : base(@"http://serverapps10.esri.com/ArcGIS/rest/services", "user1", "pass.word1") 
            // these credentials are from the Esri samples before you complain :)
        { }
    }

    public class Serializer : ISerializer
    {
        public Dictionary<String, String> AsDictionary<T>(T objectToConvert) where T : CommonParameters
        {
            return objectToConvert == null ?
                null :
                JsonSerializer.DeserializeFromString<Dictionary<String, String>>(JsonSerializer.SerializeToString(objectToConvert));
        }

        public T AsPortalResponse<T>(String dataToConvert) where T : PortalResponse
        {
            return String.IsNullOrWhiteSpace(dataToConvert) ?
                null :
                JsonSerializer.DeserializeFromString<T>(dataToConvert);
        }
    }

    public class ArcGISGatewayTests
    {
        [Fact]
        public async Task CanPingServer()
        {
            var gateway = new ArcGISGateway();

            var endpoint = new ArcGISServerEndpoint("/");

            await gateway.Ping(endpoint);
        }

        [Fact]
        public async Task QueryCanReturnFeatures()
        {
            var gateway = new ArcGISGateway();

            var query = new Query(@"/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint());
            var result = await gateway.Query<Point>(query);

            Assert.True(result.Any());
        }

        [Fact]
        public async Task QueryCanReturnDifferentGeometryTypes()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint());
            var resultPoint = await gateway.Query<Point>(queryPoint);

            Assert.True(resultPoint.Any());
            Assert.True(resultPoint.All(i => i.Geometry != null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = "lengthkm" };
            var resultPolyline = await gateway.Query<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Any());
            Assert.True(resultPolyline.All(i => i.Geometry != null));

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint()) { Where = "areasqkm = 0.012", OutFields = "areasqkm" };
            var resultPolygon = await gateway.Query<Polygon>(queryPolygon);

            Assert.True(resultPolygon.Any());
            Assert.True(resultPolygon.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task QueryCanReturnNoGeometry()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { ReturnGeometry = false };
            var resultPoint = await gateway.Query<Point>(queryPoint);

            Assert.True(resultPoint.Any());
            Assert.True(resultPoint.All(i => i.Geometry == null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = "lengthkm", ReturnGeometry = false };
            var resultPolyline = await gateway.Query<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Any());
            Assert.True(resultPolyline.All(i => i.Geometry == null));
        }

        [Fact]
        public async Task QueryOutFieldsAreHonored()
        {
            var gateway = new ArcGISGateway();

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = "lengthkm", ReturnGeometry = false };
            var resultPolyline = await gateway.Query<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Any());
            Assert.True(resultPolyline.All(i => i.Geometry == null));
            Assert.True(resultPolyline.All(i => i.Attributes != null && i.Attributes.Count == 1));

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint())
                {
                    Where = "areasqkm = 0.012", 
                    OutFields = "areasqkm,elevation,resolution,reachcode"
                };
            var resultPolygon = await gateway.Query<Polygon>(queryPolygon);

            Assert.True(resultPolygon.Any());
            Assert.True(resultPolygon.All(i => i.Geometry != null));
            Assert.True(resultPolygon.All(i => i.Attributes != null && i.Attributes.Count == 4));
        }

        [Fact]
        public async Task CanGenerateToken()
        {
            var gateway = new SecureGISGateway();

            var endpoint = new ArcGISServerEndpoint("Oil/MapServer");

            await gateway.Ping(endpoint);
        }
    }
}