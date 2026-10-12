from django.urls import path
from .views import NegocioListView

urlpatterns = [
    path('negocios/', NegocioListView.as_view(), name='lista'),
]