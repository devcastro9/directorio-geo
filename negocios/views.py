from django.db.models import Q
from django.views.generic import ListView
from .models import Negocio


class NegocioListView(ListView):
    model = Negocio
    template_name = 'lista.html'
    context_object_name = 'negocios'

    def get_queryset(self):
        qs = Negocio.objects.filter(activo=True)
        q = self.request.GET.get("q", "").strip()
        ciudad = self.request.GET.get("ciudad", "").strip()
        if q:
            qs = qs.filter(Q(nombre__icontains=q) | Q(descripcion__icontains=q))
        if ciudad:
            qs = qs.filter(ciudad__iexact=ciudad)
        return qs

    def get_context_data(self, **kwargs):
        ctx = super().get_context_data(**kwargs)
        ctx["q"] = self.request.GET.get("q", "")
        ctx["ciudad"] = self.request.GET.get("ciudad", "")
        return ctx